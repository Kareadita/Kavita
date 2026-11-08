using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
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
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Kobo;
using Kavita.Models.Entities.Progress;
using Kavita.Models.Entities.User;
using Kavita.Services.Extensions;
using Kavita.Services.Helpers;
using Kavita.Services.Helpers.Kobo;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    IKepubifyConverterService kepubifyConverterService,
    IFileSystem fileSystem,
    ILocalizationService localizationService
    ): IKoboSyncService
{
    private static readonly TimeSpan ProgressConversionWait = TimeSpan.FromSeconds(30);

    #region Sync

    public async Task<(List<KoboSyncItem>, bool)> GetChangesSinceAsync(int userId, AppUserKoboSyncPreferences syncPreferences, KoboSyncPoint syncPoint, CancellationToken cancellationToken)
    {
        logger.LogTrace("GetChangesSinceAsync: {@SyncPoint}", syncPoint);

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

        var eligible = await KoboEligibleChapters(userId, ageRestriction, syncPreferences)
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

        var chapters = await KoboEligibleChapters(userId, ageRestriction, syncPreferences)
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

        var metadata = await BuildBookMetadata(userId, chapters.Select(s => s.Chapter.Id), cancellationToken);

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
            return KoboEligibleChapters(userId, ageRestriction, syncPreferences)
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

    private IQueryable<Chapter> KoboEligibleChapters(int userId, AgeRestriction ageRestriction, AppUserKoboSyncPreferences syncPreferences)
    {
        return unitOfWork.DataContext.Chapter
            .RestrictByLibrary(unitOfWork.DataContext.Library.GetUserLibraries(userId))
            .RestrictAgainstAgeRestriction(ageRestriction)
            .FilterByKoboSyncSettings(syncPreferences)
            .Includes(ChapterIncludes.Files);
    }

    #endregion

    #region Metadata

    public async Task<Dictionary<int, KoboBookMetadataDto>> BuildBookMetadata(int userId, IEnumerable<int> chapterIds, CancellationToken cancellationToken)
    {
        var request = httpContextAccessor.HttpContext?.Request;
        if (request is null)
        {
            throw new KavitaException();
        }

        var syncPreferences = await unitOfWork.UserRepository.GetKoboSyncPreferences(userId, cancellationToken);

        var serverSettings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(cancellationToken);
        var instanceUrl = request.GetInstanceUrl(serverSettings);

        var authKey = userContext.AuthKey();
        if (string.IsNullOrEmpty(authKey))
        {
            throw new KavitaException();
        }

        var metadata = await unitOfWork.DataContext.Chapter
            .FilterByKoboSyncSettings(syncPreferences)
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
}
