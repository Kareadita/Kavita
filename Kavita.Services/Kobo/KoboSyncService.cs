using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using HtmlAgilityPack;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.Reading;
using Kavita.API.Store;
using Kavita.Common;
using Kavita.Common.Extensions;
using Kavita.Database.Extensions;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.Kobo;
using Kavita.Models.DTOs.Progress;
using Kavita.Models.DTOs.Reader;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Kobo;
using Kavita.Models.Entities.Progress;
using Kavita.Models.Entities.User;
using Kavita.Models.Extensions;
using Kavita.Services.Extensions;
using Kavita.Services.Helpers;
using Kavita.Services.Helpers.Kobo;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using VersOne.Epub;

namespace Kavita.Services.Kobo;

internal sealed record KoboChapterSyncData
{
    public required Chapter Chapter { get; init; }
    public AppUserProgress? ReadingProgress { get; init; }
}

internal sealed record SyncAccumulationResult(List<KoboSyncItem> Results, bool Exhausted);

public class KoboSyncService(
    ILogger<KoboSyncService> logger,
    IUnitOfWork unitOfWork,
    IMapper mapper,
    IReaderService readerService,
    IBookService bookService,
    IReadingSessionService readingSessionService,
    IUserContext userContext,
    IHttpContextAccessor httpContextAccessor,
    IHttpClientFactory httpClientFactory,
    IKepubifyConverterService kepubifyConverterService,
    IFileSystem fileSystem,
    ILocalizationService localizationService
    ): IKoboSyncService
{
    private static readonly TimeSpan ProgressConversionWait = TimeSpan.FromSeconds(30);

    #region Sync

    public async Task<(List<KoboSyncItem>, bool)> GetChangesSinceAsync(int userId, AppUserKoboSyncPreferences syncPreferences, KoboSyncPoint syncPoint, CancellationToken cancellationToken)
    {
        logger.LogTrace("GetChangesSinceAsync: sync point {SyncPointId}, chapter index {ChapterIndex}, progress index {ProgressIndex}",
            syncPoint.Id, syncPoint.ChapterIndex, syncPoint.ProgressSyncedUntilIndex);

        var syncResults = new List<KoboSyncItem>();

        var ageRestriction = await unitOfWork.DataContext.AppUser.GetUserAgeRestriction(userId, cancellationToken);

        var (chapterChanges, chaptersExhausted) = await AccumulateChapterChanges(userId, syncPreferences, syncPoint, ageRestriction, cancellationToken);
        var (progressChanges, progressExhausted) = await AccumulateProgressChanges(userId, syncPreferences, syncPoint, ageRestriction, cancellationToken);

        syncResults.AddRange(chapterChanges);
        syncResults.AddRange(progressChanges);

        var localChangesExhausted = chaptersExhausted && progressExhausted;

        // All exhausted = sync done
        if (localChangesExhausted)
        {
            syncPoint.Reset();
        }

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync(cancellationToken);
        }

        logger.LogDebug("GetChangesSinceAsync: Accumulated {ChapterChanges} chapter changes and {ProgressChanges} progress changes",
            chapterChanges.Count, progressChanges.Count);

        return (syncResults, localChangesExhausted);
    }

    private async Task<SyncAccumulationResult> AccumulateChapterChanges(
        int userId,
        AppUserKoboSyncPreferences syncPreferences,
        KoboSyncPoint syncPoint,
        AgeRestriction ageRestriction,
        CancellationToken cancellationToken)
    {
        var limit = syncPreferences.SyncItemLimit;
        var alreadySeenChapters = syncPoint.ChapterIds.ToHashSet();

        var eligible = await KoboEligibleChapters(userId, ageRestriction, syncPreferences, syncPoint)
            .Select(c => new { c.Id, c.LastModifiedUtc }) // Reduce memory
            .ToDictionaryAsync(c => c.Id, c => c.LastModifiedUtc, cancellationToken);

        var changes = new List<KoboSyncItem>();

        var deletedIds = alreadySeenChapters.Except(eligible.Keys).ToList();
        var deletedBatch = deletedIds.Take(limit).ToList();

        changes.AddRange(deletedBatch.Select(BuildDeletedItem));
        deletedBatch.ForEach(id => syncPoint.ChapterIds.Remove(id));

        var pendingIds = eligible
            .Where(kv => !alreadySeenChapters.Contains(kv.Key) || kv.Value > syncPoint.ChapterSyncTime)
            .OrderBy(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Skip(syncPoint.ChapterIndex)
            .Select(kv => kv.Key)
            .ToList();

        if (changes.Count >= limit)
        {
            var hasMore = deletedIds.Count > deletedBatch.Count || pendingIds.Count > 0;
            return new SyncAccumulationResult(changes, !hasMore);
        }

        var idsToFetch = pendingIds.Take(limit - changes.Count).ToList();

        var chapters = await KoboEligibleChapters(userId, ageRestriction, syncPreferences, syncPoint)
            .LeftJoin(unitOfWork.DataContext.AppUserProgresses.Where(p => p.AppUserId == userId),
                c => c.Id,
                p => p.ChapterId,
                (c, p) => new KoboChapterSyncData { Chapter = c, ReadingProgress = p })
            .Where(sync => idsToFetch.Contains(sync.Chapter.Id))
            .ToListAsync(cancellationToken);

        if (chapters.Count == 0)
        {
            syncPoint.ChapterIds = [.. eligible.Keys];

            logger.LogTrace("No book changes since {BooksSyncTime}", syncPoint.ChapterSyncTime);
            return new SyncAccumulationResult(changes, true);
        }

        syncPoint.ChapterIndex += chapters.Count;
        logger.LogTrace("Adding {Count} book changes.", chapters.Count);

        var metadata = await BuildBookMetadata(chapters.Select(s => s.Chapter.Id), cancellationToken);

        foreach (var sync in chapters)
        {
            var entitlement = new KoboBookEntitlementContainerDto
            {
                BookEntitlement = new KoboBookEntitlementDto(sync.Chapter),
                BookMetadata = metadata[sync.Chapter.Id],
                ReadingState = await BuildKoboReadingStateDto(sync.Chapter, sync.ReadingProgress),
            };

            changes.Add(sync.Chapter.CreatedUtc > syncPoint.ChapterSyncTime || !alreadySeenChapters.Contains(sync.Chapter.Id)
                ? new KoboSyncItem { NewEntitlement = entitlement }
                : new KoboSyncItem { ChangedEntitlement = entitlement });
        }

        var exhausted = pendingIds.Count == idsToFetch.Count;
        if (exhausted)
        {
            syncPoint.ChapterIds = [.. eligible.Keys];
        }

        return new SyncAccumulationResult(changes, exhausted);
    }

    private static KoboSyncItem BuildDeletedItem(int id) => new()
    {
        ChangedEntitlement = new KoboBookEntitlementContainerDto
        {
            BookEntitlement = KoboBookEntitlementDto.ForDeleted(id.ToString()),
            BookMetadata = KoboBookMetadataDto.DeletedBook(id.ToString()),
            ReadingState = null
        },
    };

    private async Task<SyncAccumulationResult> AccumulateProgressChanges(int userId,
        AppUserKoboSyncPreferences syncPreferences,
        KoboSyncPoint syncPoint,
        AgeRestriction ageRestriction,
        CancellationToken cancellationToken)
    {
        var progressAtIndex = await ProgressSyncQueryable(syncPoint.ProgressSyncTime)
            .Skip(syncPoint.ProgressSyncedUntilIndex - 1)
            .Take(1)
            .Select(p => p.Chapter.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var useIndex = progressAtIndex == syncPoint.ProgressSyncedUntilId;

        var progressToSync = await ProgressSyncQueryable(syncPoint.ProgressSyncTime)
            .SkipIf(useIndex, syncPoint.ProgressSyncedUntilIndex)
            .Take(syncPreferences.SyncItemLimit)
            .ToListAsync(cancellationToken);

        if (progressToSync.Count == 0)
        {
            logger.LogTrace("No changes since {ProgressSyncTime}", syncPoint.ProgressSyncTime);
            return new SyncAccumulationResult([], true);
        }

        syncPoint.ProgressSyncedUntilId = progressToSync[^1].Chapter.Id;
        syncPoint.ProgressSyncedUntilIndex += progressToSync.Count;

        logger.LogTrace("Adding {Count} progress update changes", progressToSync.Count);

        List<KoboSyncItem> changes = [];
        foreach (var sync in progressToSync)
        {
            changes.Add(new KoboSyncItem { ChangedReadingState = new KoboWrappedReadingStateDto
            {
                ReadingState = await BuildKoboReadingStateDto(sync.Chapter,  sync.ReadingProgress),
            }});
        }

        return new SyncAccumulationResult(changes, progressToSync.Count < syncPreferences.SyncItemLimit);

        IOrderedQueryable<KoboChapterSyncData> ProgressSyncQueryable(DateTime after)
        {
            return KoboEligibleChapters(userId, ageRestriction, syncPreferences, syncPoint)
                .Join(unitOfWork.DataContext.AppUserProgresses.Where(p => p.AppUserId == userId),
                    c => c.Id,
                    p => p.ChapterId,
                    (c, p) => new KoboChapterSyncData
                    {
                        Chapter = c,
                        ReadingProgress = p,
                    })
                .Where(sync => sync.ReadingProgress!.LastModifiedUtc > after)
                .OrderBy(sync => sync.ReadingProgress!.LastModifiedUtc);
        }
    }

    private IQueryable<Chapter> KoboEligibleChapters(int userId,
        AgeRestriction ageRestriction, AppUserKoboSyncPreferences syncPreferences, KoboSyncPoint syncPoint)
    {
        return unitOfWork.DataContext.Chapter
            .RestrictByLibrary(unitOfWork.DataContext.Library.GetUserLibraries(userId))
            .RestrictAgainstAgeRestriction(ageRestriction)
            .FilterByKoboSyncSettings(syncPreferences, userId)
            .Where(c => !syncPoint.RemovedChapterIds.Contains(c.Id))
            .Includes(ChapterIncludes.Files);
    }

    #endregion

    #region Metadata

    public async Task<Dictionary<int, KoboBookMetadataDto>> BuildBookMetadata(IEnumerable<int> chapterIds, CancellationToken cancellationToken)
    {
        var request = httpContextAccessor.HttpContext?.Request;
        if (request is null)
        {
            throw new KavitaException();
        }

        var serverSettings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(cancellationToken);
        var instanceUrl = request.GetInstanceUrl(serverSettings);

        var authKey = userContext.AuthKey();
        if (string.IsNullOrEmpty(authKey))
        {
            throw new KavitaException();
        }

        var metadata = await unitOfWork.DataContext.Chapter
            .Where(c => chapterIds.Contains(c.Id))
            .ProjectTo<KoboBookMetadataDto>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);

        return metadata.ToDictionary(m => m.Id, m =>
        {
            var canKepubify = m.FileCount == 1 && kepubifyConverterService.KepubifySupported;

            return m with
            {
                DownloadUrls =
                [
                    new KoboDownloadUrlDto
                    {
                        Format = canKepubify ? KoboFormat.Kepub : KoboFormat.Epub3,
                        Size = m.FileSize,
                        Url = $"{instanceUrl}kobo/{authKey}/v1/library/{m.WorkId}/file/epub?convert={canKepubify}"
                    }
                ],
            };
        });
    }

    #endregion

    #region Annotations

    public async Task<IList<KoboAnnotationDto>> GetKoboAnnotationsAsync(int userId, IEnumerable<int> chapterIds, CancellationToken cancellationToken)
    {
        if (!kepubifyConverterService.KepubifySupported)
        {
            return [];
        }

        var highlightSlots = await unitOfWork.DataContext.AppUserPreferences
            .Where(p => p.AppUserId == userId)
            .Select(p => p.BookReaderHighlightSlots)
            .FirstAsync(cancellationToken);

        var annotations = await unitOfWork.DataContext.AppUserAnnotation
            .Where(an => chapterIds.Contains(an.ChapterId) && an.AppUserId == userId)
            .ToListAsync(cancellationToken);

        var koboAnnotations = new List<KoboAnnotationDto>();

        foreach (var grouping in annotations.GroupBy(an => an.ChapterId))
        {
            try
            {
                var koboAnnotation = await EncodeAnnotations(grouping.Key, grouping, highlightSlots, cancellationToken);
                koboAnnotations.AddRange(koboAnnotation);

                logger.LogTrace("Converted {Converted}/{Total} annotations for chapter {ChapterId}",
                    koboAnnotation.Count, grouping.Count(), grouping.Key);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to convert annotations for chapter {ChapterId}", grouping.Key);
            }
        }

        return koboAnnotations;
    }

    public async Task<IReadOnlyDictionary<int, string>> GetKoboAnnotationETagsAsync(int userId, IEnumerable<int> chapterIds, CancellationToken cancellationToken)
    {
        var ids = chapterIds.Distinct().ToList();

        var rows = await unitOfWork.DataContext.AppUserAnnotation
            .Where(an => an.AppUserId == userId && ids.Contains(an.ChapterId))
            .Select(an => new { an.ChapterId, an.LastModifiedUtc })
            .ToListAsync(cancellationToken);

        var etags = ids.ToDictionary(id => id, _ => BuildKoboAnnotationETag(0, DateTime.MinValue));
        foreach (var grouping in rows.GroupBy(row => row.ChapterId))
        {
            etags[grouping.Key] = BuildKoboAnnotationETag(grouping.Count(), grouping.Max(row => row.LastModifiedUtc));
        }

        return etags;
    }

    // Bump when the served annotation shape or ids change, so devices pull again
    internal const int EtagVersion = 3;

    private static string BuildKoboAnnotationETag(int count, DateTime maxLastModifiedUtc)
    {
        return $"W/\"{EtagVersion}-{count}-{(count == 0 ? 0 : maxLastModifiedUtc.Ticks)}\"";
    }

    public async Task UpdateKoboAnnotationsAsync(int userId, int chapterId, KoboPatchAnnotationsDto patch, CancellationToken cancellationToken)
    {
        if (patch is null) return;

        var annotationDtos = patch.UpdatedAnnotations ?? [];
        var deletedAnnotationIds = patch.DeletedAnnotationIds ?? [];
        if (annotationDtos.Count == 0 && deletedAnnotationIds.Count == 0) return;

        if (!await unitOfWork.UserRepository.HasAccessToChapter(userId, chapterId, cancellationToken))
        {
            logger.LogWarning("User {UserId} does not have access to chapter {ChapterId} for Kobo annotations", userId, chapterId);
            return;
        }

        var annotations = await unitOfWork.DataContext.AppUserAnnotation
            .Where(an => an.ChapterId == chapterId && an.AppUserId == userId)
            .ToListAsync(cancellationToken);

        var changed = false;

        foreach (var deletedId in deletedAnnotationIds)
        {
            var annotation = FindAnnotation(annotations, deletedId);
            if (annotation is null) continue;

            annotations.Remove(annotation);
            unitOfWork.AnnotationRepository.Remove(annotation);
            changed = true;
        }

        if (annotationDtos.Count > 0)
        {
            changed |= await ApplyKoboAnnotationUpdates(userId, chapterId, annotationDtos, annotations, cancellationToken);
        }

        if (changed)
        {
            await unitOfWork.CommitAsync(cancellationToken);
        }
    }

    private async Task<bool> ApplyKoboAnnotationUpdates(int userId, int chapterId, List<KoboAnnotationDto> annotationDtos,
        List<AppUserAnnotation> annotations, CancellationToken cancellationToken)
    {
        var chapter = await unitOfWork.ChapterRepository.GetChapterAsync(chapterId, ct: cancellationToken);
        if (chapter is null || chapter.Files.Count != 1)
        {
            logger.LogWarning("Chapter {ChapterId} must have exactly one file to sync Kobo annotations", chapterId);
            return false;
        }

        var parentIds = await unitOfWork.ChapterRepository.GetParentIdsForChapter(chapterId, cancellationToken);
        if (parentIds is null) return false;

        var epubPath = chapter.Files.First().FilePath;
        var kepubPath = kepubifyConverterService.BuildKepubifyPath(chapterId);
        if (!fileSystem.File.Exists(kepubPath)
            && !await kepubifyConverterService.ConvertEpubToKepub(epubPath, kepubPath, cancellationToken))
        {
            logger.LogWarning("Could not build a kepub for chapter {ChapterId}, Kobo annotations were not synced", chapterId);
            return false;
        }

        var highlightSlots = await unitOfWork.DataContext.AppUserPreferences
            .Where(p => p.AppUserId == userId)
            .Select(p => p.BookReaderHighlightSlots)
            .FirstAsync(cancellationToken);

        ICollection<BookChapterItem> toc = [];
        try
        {
            toc = await bookService.GenerateTableOfContents(chapter, cancellationToken);
        }
        catch (KavitaException)
        {
            /* Swallow */
        }

        var (volumeId, seriesId, libraryId) = parentIds.Value;
        var documentsByFile = new Dictionary<string, (HtmlDocument Kepub, HtmlDocument Reader, int PageNumber)>(StringComparer.Ordinal);
        var changed = false;

        foreach (var dto in annotationDtos)
        {
            var span = dto.Location?.Span;
            if (span is null || string.IsNullOrWhiteSpace(span.ChapterFilename)
                || string.IsNullOrWhiteSpace(span.StartPath) || string.IsNullOrWhiteSpace(span.EndPath)
                || string.IsNullOrWhiteSpace(dto.HighlightedText))
            {
                logger.LogDebug("Skipping Kobo annotation {AnnotationId}: missing location or highlighted text", dto.Id);
                continue;
            }

            if (!documentsByFile.TryGetValue(span.ChapterFilename, out var documents))
            {
                var (kepubDoc, pageNumber) = await bookService.GetDocumentAsync(kepubPath, span.ChapterFilename, null);
                if (kepubDoc is null)
                {
                    logger.LogWarning("Kobo annotation {AnnotationId} references unknown file {File} in chapter {ChapterId}",
                        dto.Id, span.ChapterFilename, chapterId);
                    continue;
                }

                var (readerDoc, _) = await bookService.GetDocumentAsync(epubPath, span.ChapterFilename, pageNumber);
                if (readerDoc is null)
                {
                    logger.LogWarning("Kobo annotation {AnnotationId} could not be matched to {File} in the original epub",
                        dto.Id, span.ChapterFilename);
                    continue;
                }

                documents = (kepubDoc, readerDoc, pageNumber);
                documentsByFile[span.ChapterFilename] = documents;
            }

            var startXPath = KoboPositionHelper.Decode(documents.Kepub, documents.Reader, GetKoboSpanId(span.StartPath));
            var endXPath = KoboPositionHelper.Decode(documents.Kepub, documents.Reader, GetKoboSpanId(span.EndPath));
            if (string.IsNullOrEmpty(startXPath) || string.IsNullOrEmpty(endXPath))
            {
                logger.LogDebug("Skipping Kobo annotation {AnnotationId}: could not translate its spans", dto.Id);
                continue;
            }

            var annotation = FindAnnotation(annotations, dto.Id);

            var isNew = annotation is null;
            if (isNew)
            {
                annotation = new AppUserAnnotation
                {
                    ChapterId = chapterId,
                    VolumeId = volumeId,
                    SeriesId = seriesId,
                    LibraryId = libraryId,
                    AppUserId = userId,
                    XPath = startXPath,
                };
                annotations.Add(annotation);
                unitOfWork.AnnotationRepository.Attach(annotation);
            }

            annotation.XPath = startXPath;
            annotation.EndingXPath = endXPath;
            annotation.SelectedText = dto.HighlightedText;
            annotation.HighlightCount = dto.HighlightedText.Length;
            annotation.SelectedSlotIndex = GetHighlightSlotIndex(highlightSlots, dto.HighlightColor);
            annotation.PageNumber = documents.PageNumber;
            annotation.ChapterTitle = GetChapterTitle(toc, documents.PageNumber);
            annotation.Comment = BuildComment(dto.noteText);
            annotation.CommentHtml = BuildCommentHtml(dto.noteText);
            annotation.CommentPlainText = string.IsNullOrWhiteSpace(dto.noteText) ? null : dto.noteText;
            if (string.IsNullOrEmpty(annotation.KoboId))
            {
                annotation.KoboId = string.IsNullOrEmpty(dto.Id) ? Guid.NewGuid().ToString() : dto.Id;
            }

            if (!isNew)
            {
                unitOfWork.AnnotationRepository.Update(annotation);
            }

            changed = true;
        }

        return changed;
    }

    private static AppUserAnnotation? FindAnnotation(List<AppUserAnnotation> annotations, string? annotationId)
    {
        if (string.IsNullOrEmpty(annotationId)) return null;

        return annotations.FirstOrDefault(a => a.KoboId == annotationId)
               ?? annotations.FirstOrDefault(a => a.Id.ToString() == annotationId);
    }

    private static string GetKoboSpanId(string path)
    {
        var hashIndex = path.LastIndexOf('#');
        var spanId = hashIndex >= 0 ? path[(hashIndex + 1)..] : path;
        return spanId.Replace("\\", string.Empty);
    }

    private static int GetHighlightSlotIndex(List<HighlightSlot> highlightSlots, string? koboColor)
    {
        if (string.IsNullOrEmpty(koboColor)) return 0;

        // Kobo sends #RRGGBB, tolerate an alpha suffix
        var color = koboColor.Length == 9 ? koboColor[..7] : koboColor;

        for (var i = 0; i < highlightSlots.Count; i++)
        {
            if (highlightSlots[i].Color.ToHex(false).Equals(color, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return 0;
    }

    private static string GetChapterTitle(ICollection<BookChapterItem> toc, int pageNumber)
    {
        var pageTocs = BookChapterItemHelper.GetTocForPage(toc, pageNumber);
        return pageTocs.Count > 0 ? pageTocs[0].Title : string.Empty;
    }

    private static string? BuildComment(string? noteText)
    {
        if (string.IsNullOrWhiteSpace(noteText)) return null;

        return JsonSerializer.Serialize(new
        {
            ops = new[] { new { insert = noteText + "\n" } }
        });
    }

    private static string? BuildCommentHtml(string? noteText)
    {
        return string.IsNullOrWhiteSpace(noteText) ? null : $"<p>{HtmlEncoder.Default.Encode(noteText)}</p>";
    }

    private async Task<List<KoboAnnotationDto>> EncodeAnnotations(int chapterId, IEnumerable<AppUserAnnotation> annotations, List<HighlightSlot> highlightSlots, CancellationToken cancellationToken)
    {
        var kepubPath = kepubifyConverterService.BuildKepubifyPath(chapterId);
        if (!fileSystem.File.Exists(kepubPath))
        {
            var files = await unitOfWork.ChapterRepository.GetFilesForChapterAsync(chapterId, cancellationToken);
            if (files.Count != 1) return [];

            var epubPath = files[0].FilePath;
            if (!await kepubifyConverterService.ConvertEpubToKepub(epubPath, kepubPath, cancellationToken))
            {
                return [];
            }
        }

        using var kepub = await EpubReader.OpenBookAsync(kepubPath, BookService.LenientBookReaderOptions);
        if (kepub == null) return [];

        var annotationGroups = annotations
            .GroupBy(an => an.PageNumber)
            .ToDictionary(g => g.Key, g => g);

        var koboAnnotations = new List<KoboAnnotationDto>();

        var counter = 0;
        foreach (var fileRef in await kepub.GetReadingOrderAsync())
        {
            if (!annotationGroups.TryGetValue(counter, out var grouping) || fileRef.ContentType != EpubContentType.XHTML_1_1)
            {
                counter++;
                continue;
            }

            var content = await fileRef.ReadContentAsync();

            var doc = new HtmlDocument {OptionFixNestedTags = true};
            doc.LoadHtml(BookService.EscapeTags(content));

            var converted = AnnotationHelper.BuildKoboAnnotations(fileRef.FilePath, doc, grouping, highlightSlots).ToList();
            koboAnnotations.AddRange(converted);

            logger.LogTrace("Converted {Converted}/{Total} annotations for chapter {ChapterId}@{PageNumber}",
                converted.Count, grouping.Count(), chapterId, counter);

            counter++;
        }

        return koboAnnotations;
    }

    #endregion

    #region Progress

    public async Task<KoboReadingStateDto?> GetProgress(int userId, int chapterId, CancellationToken cancellationToken)
    {
        var chapter = await unitOfWork.ChapterRepository.GetChapterAsync(chapterId, ct: cancellationToken);
        if (chapter is null)
        {
            return null;
        }

        var progress = await unitOfWork.AppUserProgressRepository.GetUserProgressAsync(chapterId, userId, cancellationToken);
        return await BuildKoboReadingStateDto(chapter, progress);
    }

    public async Task<KoboStateUpdateResultDto> SaveProgress(int userId, int chapterId, KoboReadingStateDto readingStateDto,
        CancellationToken cancellationToken)
    {
        try
        {
            await SaveProgressInner(userId, chapterId, readingStateDto, cancellationToken);
        }
        catch(Exception ex)
        {
            logger.LogError(ex, "Failed to update progress from Kobo");

            return new KoboStateUpdateResultDto
            {
                RequestResult = KoboRequestResult.Failure,
                UpdateResults =
                [
                    new KoboReadingStateUpdateResultDto(KoboRequestResult.Failure)
                    {
                        EntitlementId = chapterId.ToString(),
                    }
                ]
            };
        }

        return new KoboStateUpdateResultDto
        {
            RequestResult = KoboRequestResult.Success,
            UpdateResults =
            [
                new KoboReadingStateUpdateResultDto(KoboRequestResult.Success, KoboRequestResult.Ignored,
                    KoboRequestResult.Success)
                {
                    EntitlementId = chapterId.ToString(),
                }
            ]
        };
    }

    public async Task SyncXPathToKoboSpan(int progressId, CancellationToken cancellationToken)
    {
        if (!kepubifyConverterService.KepubifySupported)
        {
            logger.LogTrace("KoboSpan will not be synced, as kepubify is not supported");
            return;
        }

        var progress = await unitOfWork.DataContext.AppUserProgresses.FirstOrDefaultAsync(p => p.Id  == progressId, cancellationToken);
        if (progress == null || string.IsNullOrEmpty(progress.BookScrollId)) return;

        var chapter = await unitOfWork.ChapterRepository.GetChapterAsync(progress.ChapterId, ct: cancellationToken);
        if (chapter == null || chapter.Files.Count == 0) return;

        if (chapter.Files.First().Format != MangaFormat.Epub)
        {
            return;
        }

        var epubPath = chapter.Files.First().FilePath;
        var kepubPath = kepubifyConverterService.BuildKepubifyPath(chapter.Id);
        if (!fileSystem.File.Exists(kepubPath) && !await kepubifyConverterService.ConvertEpubToKepub(epubPath, kepubPath, cancellationToken)) return;

        var (readerDoc, _) = await bookService.GetDocumentAsync(epubPath, string.Empty, progress.PagesRead);
        if (readerDoc == null) return;

        var (kepubDoc, _) = await bookService.GetDocumentAsync(kepubPath, string.Empty, progress.PagesRead);
        if (kepubDoc == null) return;

        progress.KoboSpan = KoboPositionHelper.Encode(readerDoc, kepubDoc, progress.BookScrollId);

        logger.LogTrace("Sync KoboSpan with XPath. {XPath} => {KoboSpan}", progress.BookScrollId.Sanitize(), progress.KoboSpan.Sanitize());

        await unitOfWork.CommitAsync(cancellationToken);
    }


    private async Task SaveProgressInner(int userId, int chapterId, KoboReadingStateDto readingStateDto, CancellationToken cancellationToken)
    {
        var chapter = await unitOfWork.ChapterRepository.GetChapterAsync(chapterId, ct: cancellationToken);
        if (chapter == null) throw new KavitaNotFoundException();

        var parentIds = await unitOfWork.ChapterRepository.GetParentIdsForChapter(chapterId, cancellationToken);
        if (parentIds is null)
        {
            throw new KavitaNotFoundException();
        }

        var userProgressDto = await unitOfWork.AppUserProgressRepository.GetUserProgressDtoAsync(chapterId, userId, cancellationToken);
        if (userProgressDto == null)
        {
            userProgressDto = new ProgressDto
            {
                VolumeId = parentIds.Value.VolumeId,
                ChapterId = chapterId,
                PageNum = 0,
                SeriesId = parentIds.Value.SeriesId,
                LibraryId = parentIds.Value.LibraryId,
            };
        }

        var startPage = userProgressDto.PageNum;

        await UpdateProgressDto(userProgressDto, readingStateDto, chapter, cancellationToken);

        var success = await readerService.SaveReadingProgress(userProgressDto, userId, false, cancellationToken);
        if (!success)
        {
            throw new KavitaException(await localizationService.TranslateAsync(userId, "generic-read-progress"));
        }

        await readingSessionService.GenerateReadingSessionForChapters(userId, parentIds.Value.SeriesId,
            new Dictionary<int, int> { [chapterId] = startPage }, cancellationToken);
    }

    /// <summary>
    /// Sets <see cref="ProgressDto.PageNum"/>, <see cref="ProgressDto.KoboSpan"/>
    /// <see cref="ProgressDto.BookScrollId"/> is set as best as possible. Requires opening both the epub and kepub, the epub will be converted
    /// if required. While this might be a bit expensive; the conversion is cached and the kobo only sends syncs on book close
    /// (TODO: Confirm, I might have seen syncs when opening some menu's)
    /// </summary>
    /// <param name="progress"></param>
    /// <param name="readingState"></param>
    /// <param name="chapter"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="KavitaException"></exception>
    private async Task UpdateProgressDto(ProgressDto progress, KoboReadingStateDto readingState, Chapter chapter, CancellationToken cancellationToken)
    {
        if (readingState.StatusInfo.Status == KoboReadStatus.Finished)
        {
            progress.PageNum = chapter.Pages;
            progress.BookScrollId = null;
            progress.KoboSpan = null;
            return;
        }

        var file = readingState.CurrentBookmark.Location?.Source;
        // Start of book
        if (string.IsNullOrEmpty(file))
        {
            progress.PageNum = 0;
            progress.BookScrollId = null;
            progress.KoboSpan = null;
            return;
        }

        if (chapter.Files.First().Format != MangaFormat.Epub)
        {
            var page = fileSystem.Path.GetFileNameWithoutExtension(file).TrimPrefix("section");
            if (int.TryParse(page, out var pageNumber))
            {
                progress.PageNum = pageNumber - 1; // sections are 1 indexed
                progress.BookScrollId = null;
                progress.KoboSpan = null;
            }
            return;
        }

        if (!kepubifyConverterService.KepubifySupported)
        {
            logger.LogTrace("Kepubify is not supported, progress will be chapter at best");
            await SetFallbackProgress();
            return;
        }

        if (chapter.Files.Count != 1)
        {
            throw new KavitaException();
        }

        var kepubPath = kepubifyConverterService.BuildKepubifyPath(chapter.Id);
        if (!fileSystem.File.Exists(kepubPath) && !await WaitForKepub(chapter.Files.First().FilePath, kepubPath, cancellationToken))
        {
            await SetFallbackProgress();
            return;
        }

        var (kepubDoc, pageNum) = await bookService.GetDocumentAsync(kepubPath, file, null);
        if (kepubDoc == null)
        {
            logger.LogWarning("Kepub was not available after conversion");
            await SetFallbackProgress();
            return;
        }

        var (readerDoc, _) = await bookService.GetDocumentAsync(chapter.Files.First().FilePath, file, pageNum);
        if (readerDoc == null)
        {
            logger.LogWarning("Original epub could not be opened, not saving xpath");
            await SetFallbackProgress();
            return;
        }

        progress.PageNum = pageNum;
        progress.KoboSpan = readingState.CurrentBookmark.Location?.Value;
        if (progress.KoboSpan is not null)
        {
            progress.BookScrollId = KoboPositionHelper.Decode(kepubDoc, readerDoc, progress.KoboSpan);
            logger.LogTrace("Synced XPath with KoboSpan. {KoboSpan} => {XPath}", progress.KoboSpan.Sanitize(), progress.BookScrollId.Sanitize());
        }

        return;

        async Task SetFallbackProgress()
        {
            var pageNum = await bookService.GetPageForFile(chapter.Files.First().FilePath, file);
            if (pageNum is null)
            {
                throw new KavitaException();
            }

            progress.PageNum = pageNum.Value;
            // Reset if we can't translate
            progress.BookScrollId = null;
            progress.KoboSpan = readingState.CurrentBookmark.Location?.Value;
        }
    }

    private async Task<bool> WaitForKepub(string epubPath, string kepubPath, CancellationToken cancellationToken)
    {
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        wait.CancelAfter(ProgressConversionWait);

        try
        {
            return await kepubifyConverterService.ConvertEpubToKepub(epubPath, kepubPath, wait.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>
    /// Prevent duplicate book opens during syncing
    /// </summary>
    private readonly Dictionary<int, KoboReadingStateDto> _cachedReadingStateDtos = [];

    private async Task<KoboReadingStateDto> BuildKoboReadingStateDto(Chapter chapter, AppUserProgress? progress)
    {
        if (_cachedReadingStateDtos.TryGetValue(chapter.Id, out var readingStateDto))
        {
            return readingStateDto;
        }

        if (progress is null)
        {
            var created = chapter.Created;
            return new KoboReadingStateDto
            {
                EntitlementId = chapter.Id.ToString(),
                Created = created,
                LastModified = created,
                PriorityTimestamp = created,
                CurrentBookmark = new KoboBookmarkDto { LastModified = created, },
                Statistics = new KoboStatisticsDto { LastModified = created },
                StatusInfo = new KoboStatusInfoDto
                {
                    LastModified = created,
                    Status = KoboReadStatus.ReadyToRead,
                    TimesStartedReading = 0,
                }
            };
        }


        var currentProgress = (float) Math.Ceiling((progress.PagesRead / (float)chapter.Pages) * 100);
        KoboReadStatus readStatus;
        if (progress.PagesRead == 0)
        {
            readStatus = KoboReadStatus.ReadyToRead;
        }
        else if (progress.PagesRead == chapter.Pages)
        {
            readStatus = KoboReadStatus.Finished;
        }
        else
        {
            readStatus = KoboReadStatus.Reading;
        }

        var state = new KoboReadingStateDto
        {
            EntitlementId = chapter.Id.ToString(),
            Created = progress.Created,
            LastModified = progress.LastModified,
            PriorityTimestamp = progress.LastModified,
            CurrentBookmark = new KoboBookmarkDto
            {
                LastModified = progress.LastModified,
                ProgressPercent = currentProgress,
                ContentSourceProgressPercent = 0, // We cannot track this
                Location = await GetKoboPosition(progress, chapter)
            },
            Statistics = new KoboStatisticsDto { LastModified =  progress.LastModified, },
            StatusInfo = new KoboStatusInfoDto
            {
                LastModified =  progress.LastModified,
                Status = readStatus,
            }
        };

        _cachedReadingStateDtos[chapter.Id] = state;
        return state;
    }

    private async Task<KoboLocationDto> GetKoboPosition(AppUserProgress? progress, Chapter chapter)
    {
        if (progress == null || progress.PagesRead == 0 || chapter.Files.Count <= 0)
        {
            return new KoboLocationDto();
        }

        if (chapter.Files.First().Format is MangaFormat.Archive or MangaFormat.Image)
        {
            return new KoboLocationDto
            {
                Value = string.Empty,
                Source = $"EPUB/section{progress.PagesRead + 1}.html"
            };
        }

        var source = await bookService.GetFileForPage(chapter.Files.First().FilePath, progress.PagesRead);

        return new KoboLocationDto
        {
            Value = string.IsNullOrEmpty(progress.KoboSpan) ? string.Empty : progress.KoboSpan,
            Source = source,
        };
    }

    #endregion

    #region Proxy

    private static readonly IReadOnlySet<string> IncludeHeaders = new HashSet<string>
    {
        HeaderNames.Authorization,
        HeaderNames.UserAgent,
        HeaderNames.Accept,
        HeaderNames.AcceptLanguage,
        HeaderNames.ContentType,
    };

    private static bool IsKoboHeader(string header) =>
        header.StartsWith("x-kobo-", StringComparison.OrdinalIgnoreCase)
        && !header.Equals(KoboHeaders.KoboSyncToken, StringComparison.OrdinalIgnoreCase);

    private HttpContext Context => httpContextAccessor.HttpContext!;
    private HttpClient KoboHttpClient => httpClientFactory.CreateClient("Kobo");
    private Task<AppUserKoboSyncPreferences> KoboSyncSettings => unitOfWork.UserRepository.GetKoboSyncPreferences(userContext.GetUserIdOrThrow(), Context.RequestAborted);

    /// <summary>
    /// Defaults to <see cref="KoboHttpClient"/>
    /// </summary>
    /// <param name="httpClient"></param>
    /// <param name="body"></param>
    /// <returns></returns>
    public async Task<ActionResult> ProxyRequestIfEnabled(HttpClient? httpClient = null, object? body = null)
    {
        if ((await KoboSyncSettings).ForwardToKoboStore)
        {
            await ProxyRequest(null, Context.RequestAborted, body: body, httpClient: httpClient);
            return new EmptyResult();
        }

        return new NotFoundResult();
    }

    public async Task<(JsonNode?, bool)> ProxyRequest(KoboSyncPoint? syncPoint, CancellationToken ct, bool copyResponse = true, object? body = null, HttpClient? httpClient = null, bool writeResponseOnError = true)
    {
        var client = httpClient ?? KoboHttpClient;
        var request = Context.Request;
        var response = Context.Response;

        var targetUrl = KoboStoreUrl.Build(request.Path, request.QueryString, client.BaseAddress);
        var safeTargetUrl = targetUrl.AbsoluteUri.Sanitize();

        logger.LogTrace("Going to proxy request to {TargetUrl} for {UserId}", safeTargetUrl, userContext.GetUserIdOrThrow());

        using var upstreamRequest = new HttpRequestMessage(new HttpMethod(request.Method), targetUrl);

        upstreamRequest.CopyRequest(request, header => IncludeHeaders.Contains(header) || IsKoboHeader(header), body);

        upstreamRequest.Headers.Remove(KoboHeaders.KoboSyncToken);

        // Add back if requested (Need to remove first so it doesn't have 2 values)
        if (!string.IsNullOrEmpty(syncPoint?.KoboStoreSyncToken))
        {
            upstreamRequest.Headers.TryAddWithoutValidation(KoboHeaders.KoboSyncToken, syncPoint.KoboStoreSyncToken);
        }

        using var upstreamResponse = await client.SendAsync(upstreamRequest, HttpCompletionOption.ResponseHeadersRead, ct);

        logger.LogTrace("Proxied {Method} {Url} returned {StatusCode} {ContentType}",
            request.Method.Sanitize(), safeTargetUrl, (int) upstreamResponse.StatusCode, upstreamResponse.Content.Headers.ContentType?.MediaType?.Sanitize());

        if (!upstreamResponse.IsSuccessStatusCode && !writeResponseOnError)
        {
            logger.LogWarning("Failed to proxy request to {Url}: {StatusCode} {ReasonPhrase}", safeTargetUrl, upstreamResponse.StatusCode, upstreamResponse.ReasonPhrase);
            return (null, false);
        }

        // Copy sync tokens from Kobo Servers back over
        if (syncPoint is not null)
        {
            if (upstreamResponse.Headers.TryGetValues(KoboHeaders.KoboSync, out var koboSyncHeader))
            {
                response.Headers[KoboHeaders.KoboSync] = koboSyncHeader.FirstOrDefault();
            }

            if (upstreamResponse.Headers.TryGetValues(KoboHeaders.KoboSyncToken, out var koboSyncTokens))
            {
                var koboSyncTokenValue = koboSyncTokens.FirstOrDefault();
                if (!string.IsNullOrEmpty(koboSyncTokenValue))
                {
                    syncPoint.KoboStoreSyncToken = koboSyncTokenValue;
                    if (unitOfWork.HasChanges())
                    {
                        await unitOfWork.CommitAsync(ct);
                    }
                }
            }

            response.Headers[KoboHeaders.KoboSyncToken] = KoboSyncTokenHelper.Encode(syncPoint.Id);
        }

        if (!upstreamResponse.IsSuccessStatusCode || copyResponse)
        {
            if (!upstreamResponse.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to proxy request to {Url}: {StatusCode} {ReasonPhrase}", safeTargetUrl, upstreamResponse.StatusCode, upstreamResponse.ReasonPhrase);
            }

            response.Headers.XContentTypeOptions = "nosniff";
            response.Headers.Append(HeaderNames.ContentSecurityPolicy, "sandbox");

            await response.CopyResponse(upstreamResponse, IsKoboHeader, ct);
            return (null, true);
        }

        return (await upstreamResponse.Content.ReadFromJsonAsync<JsonNode>(ct), false);
    }

    #endregion
}
