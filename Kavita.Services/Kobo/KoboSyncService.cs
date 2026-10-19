using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.Reading;
using Kavita.Common;
using Kavita.Database.Extensions;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.Kobo;
using Kavita.Models.DTOs.Progress;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Kobo;
using Kavita.Models.Entities.Progress;
using Kavita.Models.Entities.User;
using Kavita.Services.Extensions;
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

internal sealed record SyncAccumulationResult(IEnumerable<KoboSyncItem> Results, bool Exhausted);

internal class KoboSyncService(
    ILogger<KoboSyncService> logger,
    IUnitOfWork unitOfWork,
    IMapper mapper,
    IReaderService readerService,
    IBookService bookService,
    IReadingSessionService readingSessionService,
    IHttpContextAccessor httpContextAccessor,
    IKepubifyConverter kepubifyConverter,
    IFileSystem fileSystem
    ): IKoboSyncService
{

    #region Sync

    public async Task<(IEnumerable<KoboSyncItem>, bool)> GetChangesSinceAsync(int userId, AppUserKoboSyncPreferences syncPreferences, KoboSyncPoint syncPoint, CancellationToken cancellationToken)
    {
        var syncResults = new List<KoboSyncItem>();

        var ageRestriction = await unitOfWork.DataContext.AppUser.GetUserAgeRestriction(userId, cancellationToken);

        var (bookChanges, booksExhausted) = await AccumulateBookChanges(userId, syncPreferences, syncPoint, ageRestriction, cancellationToken);
        var (progressChanges, progressExhausted) = await AccumulateProgressChanges(userId, syncPreferences, syncPoint, ageRestriction, cancellationToken);

        syncResults.AddRange(bookChanges);
        syncResults.AddRange(progressChanges);

        var localChangesExhausted = booksExhausted && progressExhausted;

        // All exhausted = sync done
        if (localChangesExhausted)
        {
            syncPoint.BookSyncTime = DateTime.Now;
            syncPoint.ProgressSyncTime = DateTime.Now;
        }

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync(cancellationToken);
        }

        return (syncResults, localChangesExhausted);
    }

    private async Task<SyncAccumulationResult> AccumulateBookChanges(
        int userId,
        AppUserKoboSyncPreferences syncPreferences,
        KoboSyncPoint syncPoint,
        AgeRestriction ageRestriction,
        CancellationToken cancellationToken)
    {
        // TODO: Removed books

        var bookAtIndex = await BookSyncQueryable(syncPoint.BookSyncTime)
            .Skip(syncPoint.BooksSyncedUntilIndex - 1)
            .Take(1)
            .Select(sync => (int?) sync.Chapter.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var useIndex = bookAtIndex == syncPoint.BooksSyncedUntilId;

        var booksToSync = await BookSyncQueryable(syncPoint.BookSyncTime)
            .SkipIf(useIndex, syncPoint.BooksSyncedUntilIndex)
            .Take(syncPreferences.SyncItemLimit)
            .ToListAsync(cancellationToken);

        if (booksToSync.Count == 0)
        {
            logger.LogDebug("No books changes since {BooksSyncTime}", syncPoint.BookSyncTime);
            return new SyncAccumulationResult([], true);
        }

        syncPoint.BooksSyncedUntilId = booksToSync[^1].Chapter.Id;
        syncPoint.BooksSyncedUntilIndex += booksToSync.Count;

        logger.LogDebug("Adding {Count} books changes.", booksToSync.Count);

        var metadata = await BuildBookMetadata(userId, booksToSync.Select(s => s.Chapter.Id), cancellationToken);

        List<KoboSyncItem> changes = [];
        foreach (var sync in booksToSync)
        {
            var entitlement = new KoboBookEntitlementContainerDto
            {
                BookEntitlement = new KoboBookEntitlementDto(sync.Chapter),
                BookMetadata = metadata[sync.Chapter.Id],
                ReadingState = await BuildKoboReadingStateDto(sync.Chapter, sync.ReadingProgress),
            };

            changes.Add(sync.Chapter.Created > syncPoint.BookSyncTime
                ? new KoboSyncItem { NewEntitlement = entitlement }
                : new KoboSyncItem { ChangedEntitlement = entitlement });
        }

        return new SyncAccumulationResult(changes, booksToSync.Count < syncPreferences.SyncItemLimit);

        IOrderedQueryable<KoboChapterSyncData> BookSyncQueryable(DateTime after)
        {
            return KoboEligibleChapters(userId, ageRestriction, syncPreferences)
                .LeftJoin(unitOfWork.DataContext.AppUserProgresses.Where(p => p.AppUserId == userId),
                    c => c.Id,
                    p => p.ChapterId,
                    (c, p) => new KoboChapterSyncData
                    {
                        Chapter = c,
                        ReadingProgress = p,
                    })
                .Where(sync => sync.Chapter.LastModified > after)
                .OrderBy(sync => sync.Chapter.LastModified);
        }
    }

    private async Task<SyncAccumulationResult> AccumulateProgressChanges(int userId,
        AppUserKoboSyncPreferences syncPreferences,
        KoboSyncPoint syncPoint,
        AgeRestriction ageRestriction,
        CancellationToken cancellationToken)
    {
        var progressAtIndex = await ProgressSyncQueryable(syncPoint.ProgressSyncTime)
            .Skip(syncPoint.BooksSyncedUntilIndex - 1)
            .Take(1)
            .Select(p => p.ReadingProgress!.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var useIndex = progressAtIndex == syncPoint.BooksSyncedUntilId;

        var progressToSync = await ProgressSyncQueryable(syncPoint.ProgressSyncTime)
            .SkipIf(useIndex, syncPoint.BooksSyncedUntilIndex)
            .Take(syncPreferences.SyncItemLimit)
            .ToListAsync(cancellationToken);

        if (progressToSync.Count == 0)
        {
            logger.LogDebug("No changes since {ProgressSyncTime}", syncPoint.ProgressSyncTime);
            return new SyncAccumulationResult([], true);
        }

        syncPoint.ProgressSyncedUntilId = progressToSync[^1].Chapter.Id;
        syncPoint.ProgressSyncedUntilId += progressToSync.Count;

        logger.LogDebug("Adding {Count} progress update changes", progressToSync.Count);

        List<KoboSyncItem> changes = [];
        foreach (var sync in progressToSync)
        {
         changes.Add(new KoboSyncItem { ChangedReadingState = new KoboWrappedReadingStateDto {
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
                .Where(sync => sync.ReadingProgress!.LastModified > after)
                .OrderBy(sync => sync.ReadingProgress!.LastModified);
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

        var authKey = request.RouteValues["apiKey"]?.ToString()!;

        var metadata = await unitOfWork.DataContext.Chapter
            .FilterByKoboSyncSettings(syncPreferences)
            .Where(c => chapterIds.Contains(c.Id))
            .ProjectTo<KoboBookMetadataDto>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);

        return metadata.ToDictionary(m => m.Id, m =>
        {
            var canKepubify = m.FileCount == 1 && kepubifyConverter.KepubifySupported;

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
        if (!kepubifyConverter.KepubifySupported)
        {
            return [];
        }

        var annotations = await unitOfWork.DataContext.AppUserAnnotation
            .Where(an => chapterIds.Contains(an.ChapterId))
            .ToListAsync(cancellationToken);

        var koboAnnotations = new List<KoboAnnotationDto>();

        foreach (var grouping in annotations.GroupBy(an => an.ChapterId))
        {
            var koboAnnotation = await EncodeAnnotations(grouping.Key, grouping, cancellationToken);
            koboAnnotation.AddRange(koboAnnotation);
        }

        return koboAnnotations;
    }

    public Task UpdateKoboAnnotationsAsync(int userId, int chapterId, List<KoboAnnotationDto> annotationDtos, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private async Task<List<KoboAnnotationDto>> EncodeAnnotations(int chapterId, IEnumerable<AppUserAnnotation> annotations, CancellationToken cancellationToken)
    {
        var files = await unitOfWork.ChapterRepository.GetFilesForChapterAsync(chapterId, cancellationToken);
        if (files.Count != 1)
        {
            return [];
        }

        var epubPath = files[0].FilePath;
        var kepubPath = kepubifyConverter.BuildKepubifyPath(chapterId);
        if (!fileSystem.File.Exists(kepubPath))
        {
            await kepubifyConverter.ConvertEpubToKepub(epubPath, kepubPath, cancellationToken);
        }

        using var epub = await EpubReader.OpenBookAsync(epubPath, BookService.LenientBookReaderOptions);
        using var kepub = await EpubReader.OpenBookAsync(kepubPath, BookService.LenientBookReaderOptions);
        if (epub == null || kepub == null) return [];

        var annotationGroups = annotations
            .GroupBy(an => an.PageNumber)
            .ToDictionary(g => g.Key, g => g);

        var koboAnnotations = new List<KoboAnnotationDto>();

        var counter = 0;
        foreach (var fileRef in await epub.GetReadingOrderAsync())
        {
            if (!annotationGroups.TryGetValue(counter, out var grouping))
            {
                counter++;
                continue;
            }

            if (!kepub.Content.Html.ContainsLocalFileRefWithFilePath(fileRef.FilePath))
            {
                logger.LogWarning("Skipping annotations for {ChapterId} at {HtmlPath} as it couldn't be matched inside the kepub",
                    chapterId, fileRef.FilePath);
                counter++;
                continue;
            }

            var kepubFileRef = kepub.Content.Html.GetLocalFileRefByFilePath(fileRef.FilePath);

            


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


    private async Task SaveProgressInner(int userId, int chapterId, KoboReadingStateDto readingStateDto, CancellationToken cancellationToken)
    {
        logger.LogInformation("Saving kobo progress {@Dto}", readingStateDto);
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

        await UpdateProgressDto(userProgressDto, readingStateDto, chapter);

        await readerService.SaveReadingProgress(userProgressDto, userId, false, cancellationToken);

        // We are unable to track progress within a chapter from Kobo. So we can only generate sessions for chapter updates
        if (startPage != userProgressDto.PageNum)
        {
            await readingSessionService.GenerateReadingSessionForChapters(userId, parentIds.Value.SeriesId,
                new Dictionary<int, int> { [chapterId] = startPage }, cancellationToken);
        }
    }

    private async Task UpdateProgressDto(ProgressDto progress, KoboReadingStateDto readingState, Chapter chapter)
    {
        if (readingState.StatusInfo.Status == KoboReadStatus.Finished)
        {
            progress.PageNum = chapter.Pages;
            progress.BookScrollId = null;
            return;
        }

        var file = readingState.CurrentBookmark.Location?.Source;
        if (string.IsNullOrEmpty(file))
        {
            throw new KavitaException();
        }

        var pageNum = await bookService.GetPageForFile(chapter.Files.First().FilePath, file);
        if (pageNum is null)
        {
            throw new KavitaException();
        }

        progress.PageNum = pageNum.Value;
        // Kobo spans are untranslatable, so we store them separately. This means that syncing between Kobo and our
        // EPub-reader is at best chapter based
        progress.BookScrollId = null;
        progress.KoboSpan = readingState.CurrentBookmark.Location?.Value;
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
        if (progress == null || progress.PagesRead == 0)
        {
            return new KoboLocationDto();
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
