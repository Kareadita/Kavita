using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.Progress;
using API.DTOs.Reader;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Services.Plus;
using API.Services.Tasks;
using API.Services.Tasks.Scanner.Parser;
using API.SignalR;
using Hangfire;
using Kavita.Common;
using Microsoft.Extensions.Logging;

namespace API.Services;
#nullable enable

public interface IReaderService
{
    Task MarkSeriesAsRead(AppUser user, int seriesId);
    Task MarkSeriesAsUnread(AppUser user, int seriesId);
    Task MarkChaptersAsRead(AppUser user, int seriesId, IList<Chapter> chapters);
    Task MarkChaptersAsUnread(AppUser user, int seriesId, IList<Chapter> chapters);
    Task<bool> SaveReadingProgress(ProgressDto progressDto, int userId);
    Task<Tuple<int, int>> CapPageToChapter(int chapterId, int page);
    int CapPageToChapter(Chapter chapter, int page);
    Task<int> GetNextChapterIdAsync(int seriesId, int volumeId, int currentChapterId, int userId);
    Task<int> GetPrevChapterIdAsync(int seriesId, int volumeId, int currentChapterId, int userId);
    Task<ChapterDto> GetContinuePoint(int seriesId, int userId);
    Task MarkChaptersUntilAsRead(AppUser user, int seriesId, float chapterNumber);
    Task MarkVolumesUntilAsRead(AppUser user, int seriesId, int volumeNumber);
    HourEstimateRangeDto GetTimeEstimate(long wordCount, int pageCount, bool isEpub);
    IDictionary<int, int> GetPairs(IEnumerable<FileDimensionDto> dimensions);
    Task<string> GetThumbnail(Chapter chapter, int pageNum, IEnumerable<string> cachedImages);
}

public class ReaderService : IReaderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReaderService> _logger;
    private readonly IEventHub _eventHub;
    private readonly IImageService _imageService;
    private readonly IDirectoryService _directoryService;
    private readonly IScrobblingService _scrobblingService;
    private readonly ChapterSortComparerDefaultLast _chapterSortComparerDefaultLast = ChapterSortComparerDefaultLast.Default;
    private readonly ChapterSortComparerDefaultFirst _chapterSortComparerForInChapterSorting = ChapterSortComparerDefaultFirst.Default;
    private readonly ChapterSortComparerSpecialsLast _chapterSortComparerSpecialsLast = ChapterSortComparerSpecialsLast.Default;

    private const float MinWordsPerHour = 10260F;
    private const float MaxWordsPerHour = 30000F;
    public const float AvgWordsPerHour = (MaxWordsPerHour + MinWordsPerHour) / 2F;
    private const float MinPagesPerMinute = 3.33F;
    private const float MaxPagesPerMinute = 2.75F;
    public const float AvgPagesPerMinute = (MaxPagesPerMinute + MinPagesPerMinute) / 2F; //3.04


    public ReaderService(IUnitOfWork unitOfWork, ILogger<ReaderService> logger, IEventHub eventHub, IImageService imageService,
        IDirectoryService directoryService, IScrobblingService scrobblingService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _eventHub = eventHub;
        _imageService = imageService;
        _directoryService = directoryService;
        _scrobblingService = scrobblingService;
    }

    public static string FormatBookmarkFolderPath(string baseDirectory, int userId, int seriesId, int chapterId)
    {
        return Parser.NormalizePath(Path.Join(baseDirectory, $"{userId}", $"{seriesId}", $"{chapterId}"));
    }

    /// <summary>
    /// Does not commit. Marks all entities under the series as read.
    /// </summary>
    /// <param name="user"></param>
    /// <param name="seriesId"></param>
    public async Task MarkSeriesAsRead(AppUser user, int seriesId)
    {
        var volumes = await _unitOfWork.VolumeRepository.GetVolumes(seriesId);
        user.Progresses ??= new List<AppUserProgress>();
        foreach (var volume in volumes)
        {
            await MarkChaptersAsRead(user, seriesId, volume.Chapters);
        }
    }

    /// <summary>
    /// Does not commit. Marks all entities under the series as unread.
    /// </summary>
    /// <param name="user"></param>
    /// <param name="seriesId"></param>
    public async Task MarkSeriesAsUnread(AppUser user, int seriesId)
    {
        var volumes = await _unitOfWork.VolumeRepository.GetVolumes(seriesId);
        user.Progresses ??= new List<AppUserProgress>();
        foreach (var volume in volumes)
        {
            await MarkChaptersAsUnread(user, seriesId, volume.Chapters);
        }
    }

    /// <summary>
    /// Marks all Chapters as Read by creating or updating UserProgress rows. Does not commit.
    /// </summary>
    /// <remarks>Emits events to the UI for each chapter progress and one for each volume progress</remarks>
    /// <param name="user"></param>
    /// <param name="seriesId"></param>
    /// <param name="chapters"></param>
    public async Task MarkChaptersAsRead(AppUser user, int seriesId, IList<Chapter> chapters)
    {
        var seenVolume = new Dictionary<int, bool>();
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId);
        if (series == null) throw new KavitaException("series-doesnt-exist");

        foreach (var chapter in chapters)
        {
            var userProgress = GetUserProgressForChapter(user, chapter);

            if (userProgress == null)
            {
                user.Progresses.Add(new AppUserProgress
                {
                    PagesRead = chapter.Pages,
                    VolumeId = chapter.VolumeId,
                    SeriesId = seriesId,
                    ChapterId = chapter.Id,
                    LibraryId = series.LibraryId,
                });
            }
            else
            {
                userProgress.PagesRead = chapter.Pages;
                userProgress.SeriesId = seriesId;
                userProgress.VolumeId = chapter.VolumeId;
            }

            userProgress?.MarkModified();

            await _eventHub.SendMessageAsync(MessageFactory.UserProgressUpdate,
                MessageFactory.UserProgressUpdateEvent(user.Id, user.UserName!, seriesId, chapter.VolumeId, chapter.Id, chapter.Pages));

            // Send out volume events for each distinct volume
            if (seenVolume.TryAdd(chapter.VolumeId, true))
            {
                await _eventHub.SendMessageAsync(MessageFactory.UserProgressUpdate,
                    MessageFactory.UserProgressUpdateEvent(user.Id, user.UserName!, seriesId,
                        chapter.VolumeId, 0, chapters.Where(c => c.VolumeId == chapter.VolumeId).Sum(c => c.Pages)));
            }
        }

        _unitOfWork.UserRepository.Update(user);
    }

    /// <summary>
    /// Marks all Chapters as Unread by creating or updating UserProgress rows. Does not commit.
    /// </summary>
    /// <param name="user"></param>
    /// <param name="seriesId"></param>
    /// <param name="chapters"></param>
    public async Task MarkChaptersAsUnread(AppUser user, int seriesId, IList<Chapter> chapters)
    {
        var seenVolume = new Dictionary<int, bool>();
        foreach (var chapter in chapters)
        {
            var userProgress = GetUserProgressForChapter(user, chapter);

            if (userProgress == null) continue;

            userProgress.PagesRead = 0;
            userProgress.SeriesId = seriesId;
            userProgress.VolumeId = chapter.VolumeId;
            userProgress.MarkModified();

            await _eventHub.SendMessageAsync(MessageFactory.UserProgressUpdate,
                MessageFactory.UserProgressUpdateEvent(user.Id, user.UserName!, userProgress.SeriesId, userProgress.VolumeId, userProgress.ChapterId, 0));

            // Send out volume events for each distinct volume
            if (!seenVolume.ContainsKey(chapter.VolumeId))
            {
                seenVolume[chapter.VolumeId] = true;
                await _eventHub.SendMessageAsync(MessageFactory.UserProgressUpdate,
                    MessageFactory.UserProgressUpdateEvent(user.Id, user.UserName!, seriesId,
                        chapter.VolumeId, 0, 0));
            }
        }
        _unitOfWork.UserRepository.Update(user);
    }

    /// <summary>
    /// Gets the User Progress for a given Chapter. This will handle any duplicates that might have occured in past versions and will delete them. Does not commit.
    /// </summary>
    /// <param name="user">Must have Progresses populated</param>
    /// <param name="chapter"></param>
    /// <returns></returns>
    private AppUserProgress? GetUserProgressForChapter(AppUser user, Chapter chapter)
    {
        AppUserProgress? userProgress = null;

        if (user.Progresses == null)
        {
            throw new KavitaException("progress-must-exist");
        }

        try
        {
            userProgress =
                user.Progresses.SingleOrDefault(x => x.ChapterId == chapter.Id && x.AppUserId == user.Id);
        }
        catch (Exception)
        {
            // There is a very rare chance that user progress will duplicate current row. If that happens delete one with less pages
            var progresses = user.Progresses.Where(x => x.ChapterId == chapter.Id && x.AppUserId == user.Id).ToList();
            if (progresses.Count > 1)
            {
                var highestProgress = progresses.Max(x => x.PagesRead);
                var firstProgress = progresses.OrderBy(p => p.LastModifiedUtc).First();
                firstProgress.PagesRead = highestProgress;
                user.Progresses = [firstProgress];
                userProgress = user.Progresses.First();
                _logger.LogInformation("Trying to save progress and multiple progress entries exist, deleting and rewriting with highest progress rate: {@Progress}", userProgress);
            }
        }

        return userProgress;
    }

    /// <summary>
    /// Saves progress to DB
    /// </summary>
    /// <param name="progressDto"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<bool> SaveReadingProgress(ProgressDto progressDto, int userId)
    {
        // Don't let user save past total pages.
        var pageInfo = await CapPageToChapter(progressDto.ChapterId, progressDto.PageNum);
        progressDto.PageNum = pageInfo.Item1;
        var totalPages = pageInfo.Item2;

        try
        {
            var userProgress =
                await _unitOfWork.AppUserProgressRepository.GetUserProgressAsync(progressDto.ChapterId, userId);

            // Don't create an empty progress record if there isn't any progress. This prevents Last Read date from being updated when
            // opening a chapter
            if (userProgress == null && progressDto.PageNum == 0) return true;

            if (userProgress == null)
            {
                // Create a user object
                var userWithProgress =
                    await _unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.Progress);
                if (userWithProgress == null) return false;
                userWithProgress.Progresses ??= new List<AppUserProgress>();
                userWithProgress.Progresses.Add(new AppUserProgress
                {
                    PagesRead = progressDto.PageNum,
                    VolumeId = progressDto.VolumeId,
                    SeriesId = progressDto.SeriesId,
                    ChapterId = progressDto.ChapterId,
                    LibraryId = progressDto.LibraryId,
                    BookScrollId = progressDto.BookScrollId,
                });
                _unitOfWork.UserRepository.Update(userWithProgress);
            }
            else
            {
                userProgress.PagesRead = progressDto.PageNum;
                userProgress.SeriesId = progressDto.SeriesId;
                userProgress.VolumeId = progressDto.VolumeId;
                userProgress.LibraryId = progressDto.LibraryId;
                userProgress.BookScrollId = progressDto.BookScrollId;
                _unitOfWork.AppUserProgressRepository.Update(userProgress);
            }

            _logger.LogDebug("Saving Progress on Chapter {ChapterId} from Series {SeriesId} to {PageNum}", progressDto.ChapterId, progressDto.SeriesId, progressDto.PageNum);
            userProgress?.MarkModified();

            if (!_unitOfWork.HasChanges() || await _unitOfWork.CommitAsync())
            {
                var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
                await _eventHub.SendMessageAsync(MessageFactory.UserProgressUpdate,
                    MessageFactory.UserProgressUpdateEvent(userId, user!.UserName!, progressDto.SeriesId,
                        progressDto.VolumeId, progressDto.ChapterId, progressDto.PageNum));

                if (progressDto.PageNum >= totalPages)
                {
                    // Inform Scrobble service that a chapter is read
                    BackgroundJob.Enqueue(() => _scrobblingService.ScrobbleReadingUpdate(user.Id, progressDto.SeriesId));
                }

                BackgroundJob.Enqueue(() => _unitOfWork.SeriesRepository.ClearOnDeckRemoval(progressDto.SeriesId, userId));

                return true;
            }
        }
        catch (Exception exception)
        {
            // This can happen when the reader sends 2 events at same time, so 2 threads are inserting and one fails.
            if (exception.Message.StartsWith(
                    "The database operation was expected to affect 1 row(s), but actually affected 0 row(s)"))
                return true;
            _logger.LogError(exception, "Could not save progress");
            await _unitOfWork.RollbackAsync();
        }

        return false;
    }

    /// <summary>
    /// Ensures that the page is within 0 and total pages for a chapter. Makes one DB call.
    /// </summary>
    /// <param name="chapterId"></param>
    /// <param name="page"></param>
    /// <returns></returns>
    public async Task<Tuple<int, int>> CapPageToChapter(int chapterId, int page)
    {
        if (page < 0)
        {
            page = 0;
        }

        var totalPages = await _unitOfWork.ChapterRepository.GetChapterTotalPagesAsync(chapterId);
        if (page > totalPages)
        {
            page = totalPages;
        }

        return Tuple.Create(page, totalPages);
    }

    public int CapPageToChapter(Chapter chapter, int page)
    {
        if (page > chapter.Pages)
        {
            page = chapter.Pages;
        }

        if (page < 0)
        {
            page = 0;
        }

        return page;
    }

    private int GetNextSpecialChapter(VolumeDto volume, ChapterDto currentChapter)
    {
        if (volume.IsSpecial())
        {
            // Handle specials by sorting on their Filename aka Range
            return GetNextChapterId(volume.Chapters.OrderBy(x => x.SortOrder), currentChapter.SortOrder, dto => dto.SortOrder);
        }

        return -1;
    }


    /// <summary>
    /// Tries to find the next logical Chapter
    /// </summary>
    /// <example>
    /// V1 → V2 → V3 chapter 0 → V3 chapter 10 → V0 chapter 1 -> V0 chapter 2 -> (Annual 1 -> Annual 2) -> (SP 01 → SP 02)
    /// </example>
    /// <param name="seriesId"></param>
    /// <param name="volumeId"></param>
    /// <param name="currentChapterId"></param>
    /// <param name="userId"></param>
    /// <returns>-1 if nothing can be found</returns>
    public async Task<int> GetNextChapterIdAsync(int seriesId, int volumeId, int currentChapterId, int userId)
    {
        var volumes = await _unitOfWork.VolumeRepository.GetVolumesDtoAsync(seriesId, userId);

        var currentVolume = volumes.FirstOrDefault(v => v.Id == volumeId);
        if (currentVolume == null)
        {
            // Handle the case where the current volume is not found
            return -1;
        }

        var currentChapter = currentVolume.Chapters.FirstOrDefault(c => c.Id == currentChapterId);
        if (currentChapter == null)
        {
            // Handle the case where the current chapter is not found
            return -1;
        }

        var currentVolumeIndex = volumes.IndexOf(currentVolume);
        var chapterId = -1;

        if (currentVolume.IsSpecial())
        {
            // Handle specials by sorting on their Range
            chapterId = GetNextSpecialChapter(currentVolume, currentChapter);
            return chapterId;
        }

        if (currentVolume.IsLooseLeaf())
        {
            // Handle loose-leaf chapters
            chapterId = GetNextChapterId(currentVolume.Chapters.OrderBy(x => x.SortOrder),
                currentChapter.SortOrder,
                dto => dto.SortOrder);
            if (chapterId > 0) return chapterId;

            // Check specials next, as that is the order
            if (currentVolumeIndex + 1 >= volumes.Count) return -1; // There are no special volumes, so there is nothing

            var specialVolume = volumes[currentVolumeIndex + 1];
            if (!specialVolume.IsSpecial()) return -1;
            return specialVolume.Chapters.OrderByNatural(c => c.Range).FirstOrDefault()?.Id ?? -1;
        }

        // Check within the current volume if the next chapter within it can be next
        var chapters = currentVolume.Chapters.OrderBy(c => c.MinNumber).ToList();
        var currentChapterIndex = chapters.IndexOf(currentChapter);
        if (currentChapterIndex < chapters.Count - 1)
        {
            return chapters[currentChapterIndex + 1].Id;
        }

        // Check within the current Volume
        chapterId = GetNextChapterId(chapters, currentChapter.SortOrder, dto => dto.SortOrder);
        if (chapterId > 0) return chapterId;

        // Now check the next volume
        var nextVolumeIndex = currentVolumeIndex + 1;
        if (nextVolumeIndex < volumes.Count)
        {
            // Get the first chapter from the next volume
            chapterId = volumes[nextVolumeIndex].Chapters.MinBy(c => c.MinNumber, _chapterSortComparerForInChapterSorting)?.Id ?? -1;
            return chapterId;
        }

        // We are the last volume, so we need to check loose leaf
        if (currentVolumeIndex == volumes.Count - 1)
        {
            // Try to find the first loose-leaf chapter in this volume
            var firstLooseLeafChapter = volumes.WhereLooseLeaf().FirstOrDefault()?.Chapters.MinBy(c => c.MinNumber, _chapterSortComparerForInChapterSorting);
            if (firstLooseLeafChapter != null)
            {
                return firstLooseLeafChapter.Id;
            }
        }

        return -1;
    }

    /// <summary>
    /// Tries to find the prev logical Chapter
    /// </summary>
    /// <example>
    /// V1 ← V2 ← V3 chapter 0 ← V3 chapter 10 ← (V0 chapter 1 ← V0 chapter 2 ← SP 01 ← SP 02)
    /// </example>
    /// <param name="seriesId"></param>
    /// <param name="volumeId"></param>
    /// <param name="currentChapterId"></param>
    /// <param name="userId"></param>
    /// <returns>-1 if nothing can be found</returns>
    public async Task<int> GetPrevChapterIdAsync(int seriesId, int volumeId, int currentChapterId, int userId)
    {
        var volumes = (await _unitOfWork.VolumeRepository.GetVolumesDtoAsync(seriesId, userId)).ToList();
        var currentVolume = volumes.Single(v => v.Id == volumeId);
        var currentChapter = currentVolume.Chapters.Single(c => c.Id == currentChapterId);

        var chapterId = -1;

        if (currentVolume.IsSpecial())
        {
            // Check within Specials, if not set the currentVolume to Loose Leaf
            chapterId = GetNextChapterId(currentVolume.Chapters.OrderBy(x => x.SortOrder).Reverse(),
                currentChapter.SortOrder,
                dto => dto.SortOrder);
            if (chapterId > 0) return chapterId;
            currentVolume = volumes.Find(v => v.IsLooseLeaf());
        }

        if (currentVolume != null && currentVolume.IsLooseLeaf())
        {
            // If loose leaf, handle within the loose leaf. If not there, then set currentVolume to volumes.Last() where not LooseLeaf or Special
            var currentVolumeChapters = currentVolume.Chapters.OrderBy(x => x.SortOrder).ToList();
            chapterId = GetPrevChapterId(currentVolumeChapters,
                currentChapter.SortOrder, dto => dto.SortOrder, c => c.Id);
            if (chapterId > 0) return chapterId;
            currentVolume = volumes.FindLast(v => !v.IsLooseLeaf() && !v.IsSpecial());
            if (currentVolume != null) return currentVolume.Chapters.OrderBy(x => x.SortOrder).Last()?.Id ?? -1;
        }

        // When we started as a special and there was no loose leafs, reset the currentVolume
        if (currentVolume == null)
        {
            currentVolume = volumes.Find(v => !v.IsLooseLeaf() && !v.IsSpecial());
            if (currentVolume == null) return -1;
            return currentVolume.Chapters.OrderBy(x => x.SortOrder).Last()?.Id ?? -1;
        }

        // At this point, only need to check within the current Volume else move 1 level back

        // Check current volume
        chapterId = GetPrevChapterId(currentVolume.Chapters.OrderBy(x => x.SortOrder),
            currentChapter.SortOrder, dto => dto.SortOrder, c => c.Id);
        if (chapterId > 0) return chapterId;


        var currentVolumeIndex = volumes.IndexOf(currentVolume);
        if (currentVolumeIndex == 0) return -1;
        currentVolume = volumes[currentVolumeIndex - 1];
        if (currentVolume.IsLooseLeaf() || currentVolume.IsSpecial()) return -1;
        chapterId = currentVolume.Chapters.OrderBy(x => x.SortOrder).Last().Id;
        if (chapterId > 0) return chapterId;

        return -1;
    }

    private static int GetPrevChapterId<T>(IEnumerable<T> source, float currentValue, Func<T, float> selector, Func<T, int> idSelector)
    {
        var sortedSource = source.OrderBy(selector).ToList();
        var currentChapterIndex = sortedSource.FindIndex(x => selector(x).Is(currentValue));

        if (currentChapterIndex > 0)
        {
            return idSelector(sortedSource[currentChapterIndex - 1]);
        }

        // There is no previous chapter
        return -1;
    }

    /// <summary>
    /// Finds the chapter to continue reading from. If a chapter has progress and not complete, return that. If not, progress in the
    /// ordering (Volumes -> Loose Chapters -> Annuals -> Special) to find next chapter. If all are read, return first in order for series.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<ChapterDto> GetContinuePoint(int seriesId, int userId)
    {
        var volumes = (await _unitOfWork.VolumeRepository.GetVolumesDtoAsync(seriesId, userId)).ToList();

        var anyUserProgress =
            await _unitOfWork.AppUserProgressRepository.AnyUserProgressForSeriesAsync(seriesId, userId);

        if (!anyUserProgress)
        {
            // I think i need a way to sort volumes last
            volumes = volumes.OrderBy(v => v.MinNumber, _chapterSortComparerSpecialsLast).ToList();

            // Check if we have a non-loose leaf volume
            var nonLooseLeafNonSpecialVolume = volumes.Find(v => !v.IsLooseLeaf() && !v.IsSpecial());
            if (nonLooseLeafNonSpecialVolume != null)
            {
                return nonLooseLeafNonSpecialVolume.Chapters.MinBy(c => c.SortOrder);
            }

            // We only have a loose leaf or Special left

            var chapters = volumes.First(v => v.IsLooseLeaf() || v.IsSpecial()).Chapters
                .OrderBy(c => c.SortOrder)
                .ToList();

            // If there are specials, then return the first Non-special
            if (chapters.Exists(c => c.IsSpecial))
            {
                var firstChapter = chapters.Find(c => !c.IsSpecial);
                if (firstChapter == null)
                {
                    // If there is no non-special chapter, then return first chapter
                    return chapters[0];
                }

                return firstChapter;
            }
            // Else use normal logic
            return chapters[0];
        }

        // Loop through all chapters that are not in volume 0
        var volumeChapters = volumes
            .WhereNotLooseLeaf()
            .SelectMany(v => v.Chapters)
            .ToList();

        // NOTE: If volume 1 has chapter 1 and volume 2 is just chapter 0 due to being a full volume file, then this fails
        // If there are any volumes that have progress, return those. If not, move on.
        var currentlyReadingChapter = volumeChapters
            .OrderBy(c => c.MinNumber, _chapterSortComparerDefaultLast)
            .FirstOrDefault(chapter => chapter.PagesRead < chapter.Pages && chapter.PagesRead > 0);
        if (currentlyReadingChapter != null) return currentlyReadingChapter;

        // Order with volume 0 last so we prefer the natural order
        return FindNextReadingChapter(volumes.OrderBy(v => v.MinNumber, _chapterSortComparerDefaultLast)
                                             .SelectMany(v => v.Chapters.OrderBy(c => c.SortOrder))
                                             .ToList());
    }

    private static ChapterDto FindNextReadingChapter(IList<ChapterDto> volumeChapters)
    {
        var chaptersWithProgress = volumeChapters.Where(c => c.PagesRead > 0).ToList();
        if (chaptersWithProgress.Count <= 0) return volumeChapters[0];


        var last = chaptersWithProgress.FindLastIndex(c => c.PagesRead > 0);
        if (last + 1 < chaptersWithProgress.Count)
        {
            return chaptersWithProgress[last + 1];
        }

        var lastChapter = chaptersWithProgress[last];
        if (lastChapter.PagesRead < lastChapter.Pages)
        {
            return lastChapter;
        }

        // If the last chapter didn't fit, then we need the next chapter without full progress
        var firstChapterWithoutProgress = volumeChapters.FirstOrDefault(c => c.PagesRead < c.Pages && !c.IsSpecial);
        if (firstChapterWithoutProgress != null)
        {
            return firstChapterWithoutProgress;
        }


        // chaptersWithProgress are all read, then we need to get the next chapter that doesn't have progress
        var lastIndexWithProgress = volumeChapters.IndexOf(lastChapter);
        if (lastIndexWithProgress + 1 < volumeChapters.Count)
        {
            return volumeChapters[lastIndexWithProgress + 1];
        }

        return volumeChapters[0];
    }


    private static int GetNextChapterId(IEnumerable<ChapterDto> chapters, float currentChapterNumber, Func<ChapterDto, float> accessor)
    {
        var next = false;
        var chaptersList = chapters.ToList();
        foreach (var chapter in chaptersList)
        {
            if (next)
            {
                return chapter.Id;
            }

            var chapterNum = accessor(chapter);
            if (currentChapterNumber.Equals(chapterNum)) next = true;
        }

        return -1;
    }

    /// <summary>
    /// Marks every chapter that is sorted below the passed number as Read. This will not mark any specials as read or Volumes with a single 0 chapter.
    /// </summary>
    /// <param name="user"></param>
    /// <param name="seriesId"></param>
    /// <param name="chapterNumber"></param>
    public async Task MarkChaptersUntilAsRead(AppUser user, int seriesId, float chapterNumber)
    {
        var volumes = await _unitOfWork.VolumeRepository.GetVolumesForSeriesAsync(new List<int> { seriesId }, true);
        foreach (var volume in volumes.OrderBy(v => v.MinNumber))
        {
            var chapters = volume.Chapters
                .Where(c => !c.IsSpecial && c.MaxNumber <= chapterNumber)
                .OrderBy(c => c.MinNumber);
            await MarkChaptersAsRead(user, volume.SeriesId, chapters.ToList());
        }
    }

    public async Task MarkVolumesUntilAsRead(AppUser user, int seriesId, int volumeNumber)
    {
        var volumes = await _unitOfWork.VolumeRepository.GetVolumesForSeriesAsync(new List<int> { seriesId }, true);
        foreach (var volume in volumes.Where(v => v.MinNumber <= volumeNumber && v.MinNumber > 0).OrderBy(v => v.MinNumber))
        {
            await MarkChaptersAsRead(user, volume.SeriesId, volume.Chapters);
        }
    }

    public HourEstimateRangeDto GetTimeEstimate(long wordCount, int pageCount, bool isEpub)
    {
        if (isEpub)
        {
            var minHours = Math.Max((int) Math.Round((wordCount / MinWordsPerHour)), 0);
            var maxHours = Math.Max((int) Math.Round((wordCount / MaxWordsPerHour)), 0);

            return new HourEstimateRangeDto
            {
                MinHours = Math.Min(minHours, maxHours),
                MaxHours = Math.Max(minHours, maxHours),
                AvgHours = wordCount / AvgWordsPerHour
            };
        }

        var minHoursPages = Math.Max((int) Math.Round((pageCount / MinPagesPerMinute / 60F)), 0);
        var maxHoursPages = Math.Max((int) Math.Round((pageCount / MaxPagesPerMinute / 60F)), 0);

        return new HourEstimateRangeDto
        {
            MinHours = Math.Min(minHoursPages, maxHoursPages),
            MaxHours = Math.Max(minHoursPages, maxHoursPages),
            AvgHours = pageCount / AvgPagesPerMinute / 60F
        };
    }

    /// <summary>
    /// This is used exclusively for double page renderer. The goal is to break up all files into pairs respecting the reader.
    /// wide images should count as 2 pages.
    /// </summary>
    /// <param name="dimensions"></param>
    /// <returns></returns>
    public IDictionary<int, int> GetPairs(IEnumerable<FileDimensionDto> dimensions)
    {
        var pairs = new Dictionary<int, int>();
        var files = dimensions.ToList();
        if (files.Count == 0) return pairs;

        var pairStart = true;
        var previousPage = files[0];
        pairs.Add(previousPage.PageNumber, previousPage.PageNumber);

        foreach(var dimension in files.Skip(1))
        {
            if (dimension.IsWide)
            {
                pairs.Add(dimension.PageNumber, dimension.PageNumber);
                pairStart = true;
            }
            else
            {
                if (previousPage.IsWide || previousPage.PageNumber == 0)
                {
                    pairs.Add(dimension.PageNumber, dimension.PageNumber);
                    pairStart = true;
                }
                else
                {
                    pairs.Add(dimension.PageNumber, pairStart ? dimension.PageNumber - 1 : dimension.PageNumber);
                    pairStart = !pairStart;
                }
            }

            previousPage = dimension;
        }

        return pairs;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="chapter"></param>
    /// <param name="pageNum"></param>
    /// <param name="cachedImages"></param>
    /// <returns>Full path of thumbnail</returns>
    public async Task<string> GetThumbnail(Chapter chapter, int pageNum, IEnumerable<string> cachedImages)
    {
        var outputDirectory =
            _directoryService.FileSystem.Path.Join(_directoryService.TempDirectory, ImageService.GetThumbnailFormat(chapter.Id));
        try
        {
            var encodeFormat =
                (await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EncodeMediaAs;

            if (!Directory.Exists(outputDirectory))
            {
                var outputtedThumbnails = cachedImages
                    .Select((img, idx) =>
                        _directoryService.FileSystem.Path.Join(outputDirectory,
                            _imageService.WriteCoverThumbnail(img, $"{idx}", outputDirectory, encodeFormat)))
                    .ToArray();
                return CacheService.GetPageFromFiles(outputtedThumbnails, pageNum);
            }

            var files = _directoryService.GetFilesWithExtension(outputDirectory,
                Parser.ImageFileExtensions);
            return CacheService.GetPageFromFiles(files, pageNum);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an error when trying to get thumbnail for Chapter {ChapterId}, Page {PageNum}", chapter.Id, pageNum);
            _directoryService.ClearAndDeleteDirectory(outputDirectory);
            throw;
        }
    }

    /// <summary>
    /// Formats a Chapter name based on the library it's in
    /// </summary>
    /// <param name="libraryType"></param>
    /// <param name="includeHash">For comics only, includes a # which is used for numbering on cards</param>
    /// <param name="includeSpace">Add a space at the end of the string. if includeHash and includeSpace are true, only hash will be at the end.</param>
    /// <returns></returns>
    public static string FormatChapterName(LibraryType libraryType, bool includeHash = false, bool includeSpace = false)
    {
        switch(libraryType)
        {
            case LibraryType.Image:
            case LibraryType.Manga:
                return "Chapter" + (includeSpace ? " " : string.Empty);
            case LibraryType.Comic:
            case LibraryType.ComicVine:
                if (includeHash) {
                    return "Issue #";
                }
                return "Issue" + (includeSpace ? " " : string.Empty);
            case LibraryType.Book:
            case LibraryType.LightNovel:
                return "Book" + (includeSpace ? " " : string.Empty);
            default:
                throw new ArgumentOutOfRangeException(nameof(libraryType), libraryType, null);
        }
    }


}
