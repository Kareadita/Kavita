using System;
using System.Collections.Generic;
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
using API.Entities.Progress;
using API.Entities.User;
using API.Extensions;
using API.Helpers.Formatting;
using API.Services.Plus;
using API.Services.Tasks.Scanner.Parser;
using API.SignalR;
using Hangfire;
using Kavita.Common;
using Microsoft.Extensions.Logging;

namespace API.Services.Reading;
#nullable enable

public interface IReaderService
{
    Task MarkSeriesAsRead(AppUser user, int seriesId);
    Task MarkSeriesAsUnread(AppUser user, int seriesId);
    Task MarkChaptersAsRead(AppUser user, int seriesId, IList<Chapter> chapters);
    Task MarkChaptersAsUnread(AppUser user, int seriesId, IList<Chapter> chapters);
    Task<bool> SaveReadingProgress(ProgressDto progressDto, int userId);
    int CapPageToChapter(Chapter chapter, int page);
    Task<int> GetNextChapterIdAsync(int seriesId, int volumeId, int currentChapterId, int userId);
    Task<int> GetPrevChapterIdAsync(int seriesId, int volumeId, int currentChapterId, int userId);
    Task<ChapterDto> GetContinuePoint(int seriesId, int userId);
    Task MarkChaptersUntilAsRead(AppUser user, int seriesId, float chapterNumber);
    Task MarkVolumesUntilAsRead(AppUser user, int seriesId, int volumeNumber);
    IDictionary<int, int> GetPairs(IEnumerable<FileDimensionDto> dimensions);
    Task<string> GetThumbnail(Chapter chapter, int pageNum, IEnumerable<string> cachedImages);
    Task<RereadDto> CheckSeriesForReRead(int userId, int seriesId, int libraryId);
    Task<RereadDto> CheckVolumeForReRead(int userId, int volumeId, int seriesId, int libraryId);
    Task<RereadDto> CheckChapterForReRead(int userId, int chapterId, int seriesId, int libraryId);
}

public class ReaderService(IUnitOfWork unitOfWork, ILogger<ReaderService> logger, IEventHub eventHub, IImageService imageService,
    IDirectoryService directoryService, IScrobblingService scrobblingService, IReadingSessionService readingSessionService,
    IClientInfoAccessor clientInfoAccessor, ISeriesService seriesService, IEntityNamingService namingService,
    ILocalizationService localizationService)
    : IReaderService
{
    private readonly ChapterSortComparerDefaultLast _chapterSortComparerDefaultLast = ChapterSortComparerDefaultLast.Default;
    private readonly ChapterSortComparerDefaultFirst _chapterSortComparerForInChapterSorting = ChapterSortComparerDefaultFirst.Default;
    private readonly ChapterSortComparerSpecialsLast _chapterSortComparerSpecialsLast = ChapterSortComparerSpecialsLast.Default;

    private const float MinWordsPerHour = 10260F;
    private const float MaxWordsPerHour = 30000F;
    public const float AvgWordsPerHour = (MaxWordsPerHour + MinWordsPerHour) / 2F;
    private const float MinPagesPerMinute = 3.33F;
    private const float MaxPagesPerMinute = 2.75F;
    public const float AvgPagesPerMinute = (MaxPagesPerMinute + MinPagesPerMinute) / 2F; //3.04


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
        var volumes = await unitOfWork.VolumeRepository.GetVolumes(seriesId);
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
        var volumes = await unitOfWork.VolumeRepository.GetVolumes(seriesId);
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
        var series = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId);
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
                userProgress.TotalReads += 1;
            }

            userProgress?.MarkModified();

            await eventHub.SendMessageAsync(MessageFactory.UserProgressUpdate,
                MessageFactory.UserProgressUpdateEvent(user.Id, user.UserName!, seriesId, chapter.VolumeId, chapter.Id, chapter.Pages));

            // Send out volume events for each distinct volume
            if (seenVolume.TryAdd(chapter.VolumeId, true))
            {
                await eventHub.SendMessageAsync(MessageFactory.UserProgressUpdate,
                    MessageFactory.UserProgressUpdateEvent(user.Id, user.UserName!, seriesId,
                        chapter.VolumeId, 0, chapters.Where(c => c.VolumeId == chapter.VolumeId).Sum(c => c.Pages)));
            }
        }

        unitOfWork.UserRepository.Update(user);
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

            await eventHub.SendMessageAsync(MessageFactory.UserProgressUpdate,
                MessageFactory.UserProgressUpdateEvent(user.Id, user.UserName!, userProgress.SeriesId, userProgress.VolumeId, userProgress.ChapterId, 0));

            // Send out volume events for each distinct volume
            if (seenVolume.TryAdd(chapter.VolumeId, true))
            {
                await eventHub.SendMessageAsync(MessageFactory.UserProgressUpdate,
                    MessageFactory.UserProgressUpdateEvent(user.Id, user.UserName!, seriesId,
                        chapter.VolumeId, 0, 0));
            }
        }
        unitOfWork.UserRepository.Update(user);
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
            //throw new ArgumentException("AppUser must have Progress on it"); // TODO: Figure out the impact of switching to a more dev experience exception
            throw new KavitaException("progress-must-exist");
        }

        try
        {
            userProgress = user.Progresses.SingleOrDefault(x => x.ChapterId == chapter.Id && x.AppUserId == user.Id);
        }
        catch (Exception)
        {
            // There is a very rare chance that user progress will duplicate current row. If that happens delete one with fewer pages
            var progresses = user.Progresses.Where(x => x.ChapterId == chapter.Id && x.AppUserId == user.Id).ToList();
            if (progresses.Count > 1)
            {
                var highestProgress = progresses.Max(x => x.PagesRead);
                var firstProgress = progresses.OrderBy(p => p.LastModifiedUtc).First();
                firstProgress.PagesRead = highestProgress;
                user.Progresses = [firstProgress];
                userProgress = user.Progresses.First();
                logger.LogInformation("Trying to save progress and multiple progress entries exist, deleting and rewriting with highest progress rate: {@Progress}", userProgress);
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
            var userProgress = await unitOfWork.AppUserProgressRepository.GetUserProgressAsync(progressDto.ChapterId, userId);

            // Don't create an empty progress record if there isn't any progress. This prevents Last Read date from being updated when
            // opening a chapter
            if (userProgress == null && progressDto.PageNum == 0) return true;

            if (userProgress == null)
            {
                // Create a user object
                var userWithProgress = await unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.Progress);
                if (userWithProgress == null) return false;

                userWithProgress.Progresses ??= [];
                userProgress = new AppUserProgress
                {
                    PagesRead = progressDto.PageNum,
                    VolumeId = progressDto.VolumeId,
                    SeriesId = progressDto.SeriesId,
                    ChapterId = progressDto.ChapterId,
                    LibraryId = progressDto.LibraryId,
                    BookScrollId = progressDto.BookScrollId,
                };
                userWithProgress.Progresses.Add(userProgress);
                unitOfWork.UserRepository.Update(userWithProgress);
            }
            else
            {
                userProgress.PagesRead = progressDto.PageNum;
                userProgress.SeriesId = progressDto.SeriesId;
                userProgress.VolumeId = progressDto.VolumeId;
                userProgress.LibraryId = progressDto.LibraryId;
                userProgress.BookScrollId = progressDto.BookScrollId;
                unitOfWork.AppUserProgressRepository.Update(userProgress);
            }

            logger.LogDebug("Saving Progress on Series {SeriesId}, Chapter {ChapterId} to Page {PageNum}", progressDto.SeriesId, progressDto.ChapterId, progressDto.PageNum);
            userProgress.MarkModified();

            if (!unitOfWork.HasChanges() || await unitOfWork.CommitAsync())
            {
                BackgroundJob.Enqueue(() => readingSessionService.UpdateProgress(userId, progressDto, clientInfoAccessor.Current, clientInfoAccessor.CurrentDeviceId));

                var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId);
                await eventHub.SendMessageAsync(MessageFactory.UserProgressUpdate,
                    MessageFactory.UserProgressUpdateEvent(userId, user!.UserName!, progressDto.SeriesId,
                        progressDto.VolumeId, progressDto.ChapterId, progressDto.PageNum));

                if (progressDto.PageNum >= totalPages)
                {
                    // Inform Scrobble service that a chapter is read
                    BackgroundJob.Enqueue(() => scrobblingService.ScrobbleReadingUpdate(user.Id, progressDto.SeriesId));
                }

                BackgroundJob.Enqueue(() => unitOfWork.SeriesRepository.ClearOnDeckRemoval(progressDto.SeriesId, userId));

                return true;
            }
        }
        catch (Exception exception)
        {
            // This can happen when the reader sends 2 events at same time, so 2 threads are inserting and one fails.
            if (exception.Message.StartsWith(
                    "The database operation was expected to affect 1 row(s), but actually affected 0 row(s)"))
                return true;
            logger.LogError(exception, "Could not save progress");
            await unitOfWork.RollbackAsync();
        }

        return false;
    }

    /// <summary>
    /// Ensures that the page is within 0 and total pages for a chapter. Makes one DB call.
    /// </summary>
    /// <param name="chapterId"></param>
    /// <param name="page"></param>
    /// <returns></returns>
    private async Task<Tuple<int, int>> CapPageToChapter(int chapterId, int page)
    {
        if (page < 0)
        {
            page = 0;
        }

        var totalPages = await unitOfWork.ChapterRepository.GetChapterTotalPagesAsync(chapterId);
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

    private static int GetNextSpecialChapter(VolumeDto volume, ChapterDto currentChapter)
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
        var volumes = await unitOfWork.VolumeRepository.GetVolumesDtoAsync(seriesId, userId);

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
        var volumes = (await unitOfWork.VolumeRepository.GetVolumesDtoAsync(seriesId, userId)).ToList();
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
        // Since the first chapter has progress already on it, we can check if there is any progress and if not, return that chapter
        var firstChapter = await unitOfWork.ChapterRepository.GetFirstChapterForSeriesAsync(seriesId, userId);
        if (firstChapter is { PagesRead: 0 }) return firstChapter;

        var currentlyReading = await unitOfWork.ChapterRepository.GetCurrentlyReadingChapterAsync(seriesId, userId);
        if (currentlyReading != null) return currentlyReading;

        var volumes = (await unitOfWork.VolumeRepository.GetVolumesDtoAsync(seriesId, userId, VolumeIncludes.Files)).ToList();

        var allChapters = volumes
            .OrderBy(v => v.MinNumber, _chapterSortComparerDefaultLast)
            .SelectMany(v => v.Chapters.OrderBy(c => c.SortOrder))
            .ToList();

        return FindNextReadingChapter(allChapters);
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
        var volumes = await unitOfWork.VolumeRepository.GetVolumesForSeriesAsync(new List<int> { seriesId }, true);
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
        var volumes = await unitOfWork.VolumeRepository.GetVolumesForSeriesAsync(new List<int> { seriesId }, true);
        foreach (var volume in volumes.Where(v => v.MinNumber <= volumeNumber && v.MinNumber > 0).OrderBy(v => v.MinNumber))
        {
            await MarkChaptersAsRead(user, volume.SeriesId, volume.Chapters);
        }
    }

    public static HourEstimateRangeDto GetTimeEstimate(long wordCount, int pageCount, bool isEpub)
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
            directoryService.FileSystem.Path.Join(directoryService.TempDirectory, ImageService.GetThumbnailFormat(chapter.Id));
        try
        {
            var encodeFormat =
                (await unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EncodeMediaAs;

            if (!Directory.Exists(outputDirectory))
            {
                var outputtedThumbnails = cachedImages
                    .Select((img, idx) =>
                        directoryService.FileSystem.Path.Join(outputDirectory,
                            imageService.WriteCoverThumbnail(img, $"{idx}", outputDirectory, encodeFormat)))
                    .ToArray();
                return CacheService.GetPageFromFiles(outputtedThumbnails, pageNum);
            }

            var files = directoryService.GetFilesWithExtension(outputDirectory,
                Parser.ImageFileExtensions);
            return CacheService.GetPageFromFiles(files, pageNum);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an error when trying to get thumbnail for Chapter {ChapterId}, Page {PageNum}", chapter.Id, pageNum);
            directoryService.ClearAndDeleteDirectory(outputDirectory);
            throw;
        }
    }

    public async Task<RereadDto> CheckSeriesForReRead(int userId, int seriesId, int libraryId)
    {
        var series = await unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, userId);
        if (series == null) return RereadDto.Dont();

        var namingContext = await CreateNamingContext(userId, libraryId);

        var continuePoint = await GetContinuePoint(seriesId, userId);
        var continuePointLabel = await FormatReReadLabel(userId, namingContext, continuePoint);

        var lastProgress = await unitOfWork.AppUserProgressRepository.GetLatestProgressForSeries(seriesId, userId);

        if (lastProgress == null || !await unitOfWork.AppUserProgressRepository.AnyUserProgressForSeriesAsync(seriesId, userId))
        {
            return new RereadDto
            {
                ShouldPrompt = false,
                ChapterOnContinue = new RereadChapterDto(libraryId, seriesId, continuePoint.VolumeId, continuePoint.Id, continuePointLabel, continuePoint.Format),
            };
        }

        // Series is fully read, prompt for full reread
        if (series.PagesRead >= series.Pages)
        {
            var firstChapter = await unitOfWork.ChapterRepository.GetFirstChapterForSeriesAsync(seriesId, userId);

            if (firstChapter != null)
            {
                // We will be rereading the series, use its name as label
                return new RereadDto
                {
                    ShouldPrompt = true,
                    FullReread = true,
                    TimePrompt = false,
                    ChapterOnContinue = new RereadChapterDto(libraryId, seriesId, continuePoint.VolumeId, continuePoint.Id, continuePointLabel, continuePoint.Format),
                    ChapterOnReread = new RereadChapterDto(libraryId, seriesId, firstChapter.VolumeId, firstChapter.Id, series.Name, continuePoint.Format),
                };
            }

        }

        var userPreferences = await unitOfWork.UserRepository.GetPreferencesForUser(userId);

        return await BuildRereadDto(
            namingContext,
            userId,
            userPreferences,
            series.LibraryId,
            seriesId,
            continuePoint,
            continuePointLabel,
            lastProgress.Value,
            getPrevChapter: async () =>
            {
                var chapterId = await GetPrevChapterIdAsync(seriesId, continuePoint.VolumeId, continuePoint.Id, userId);
                if (chapterId == -1) return null;

                return await unitOfWork.ChapterRepository.GetChapterDtoAsync(chapterId, userId);
            },
            isValidPrevChapter: prevChapter => prevChapter != null
        );
    }

    public async Task<RereadDto> CheckVolumeForReRead(int userId, int volumeId, int seriesId, int libraryId)
    {
        var userPreferences = await unitOfWork.UserRepository.GetPreferencesForUser(userId);

        var volume = await unitOfWork.VolumeRepository.GetVolumeDtoAsync(volumeId, userId);
        if (volume == null) return RereadDto.Dont();

        var namingContext = await CreateNamingContext(userId, libraryId);

        var continuePoint = FindNextReadingChapter([.. volume.Chapters]);
        var continuePointLabel = await FormatReReadLabel(userId, namingContext, continuePoint);

        var lastProgress = await unitOfWork.AppUserProgressRepository.GetLatestProgressForVolume(volumeId, userId);

        // Check if there's no progress on the volume
        if (lastProgress == null || volume.PagesRead == 0)
        {
            return new RereadDto
            {
                ShouldPrompt = false,
                ChapterOnContinue = new RereadChapterDto(libraryId, seriesId, volumeId, continuePoint.Id, continuePointLabel, continuePoint.Format),
            };
        }

        // Volume is fully read, prompt for full reread
        if (volume.PagesRead >= volume.Pages)
        {
            var firstChapter = await unitOfWork.ChapterRepository.GetFirstChapterForVolumeAsync(volumeId, userId);

            if (firstChapter != null)
            {
                // We will be rereading the volume, use its name as label
                var displayName = namingContext.FormatVolumeName(volume);

                return new RereadDto
                {
                    ShouldPrompt = true,
                    FullReread = true,
                    TimePrompt = false,
                    ChapterOnContinue = new RereadChapterDto(libraryId, seriesId, volumeId, continuePoint.Id, continuePointLabel, continuePoint.Format),
                    ChapterOnReread = new RereadChapterDto(libraryId, seriesId, volumeId, firstChapter.Id, displayName, continuePoint.Format),
                };
            }
        }

        return await BuildRereadDto(
            namingContext,
            userId,
            userPreferences,
            libraryId,
            seriesId,
            continuePoint,
            continuePointLabel,
            lastProgress.Value,
            getPrevChapter: async () =>
            {
                var chapterId = await GetPrevChapterIdAsync(seriesId, continuePoint.VolumeId, continuePoint.Id, userId);
                if (chapterId == -1) return null;

                return await unitOfWork.ChapterRepository.GetChapterDtoAsync(chapterId, userId);
            },
            isValidPrevChapter: prevChapter => prevChapter != null && prevChapter.VolumeId == volume.Id
        );
    }

    private async Task<RereadDto> BuildRereadDto(
        LocalizedNamingContext namingContext,
        int userId,
        AppUserPreferences userPreferences,
        int libraryId,
        int seriesId,
        ChapterDto continuePoint,
        string continuePointLabel,
        DateTime lastProgress,
        Func<Task<ChapterDto?>> getPrevChapter,
        Func<ChapterDto?, bool> isValidPrevChapter)
    {
        var daysSinceLastProgress = (DateTime.UtcNow - lastProgress).Days;
        var reReadForTime = daysSinceLastProgress != 0 && daysSinceLastProgress > userPreferences.PromptForRereadsAfter;

        // Next up chapter has progress, re-read if it's fully read or long ago
        if (continuePoint.PagesRead > 0)
        {
            var reReadChapterDto = new RereadChapterDto(libraryId, seriesId, continuePoint.VolumeId, continuePoint.Id, continuePointLabel, continuePoint.Format);

            return new RereadDto
            {
                ShouldPrompt = continuePoint.PagesRead >= continuePoint.Pages || reReadForTime,
                TimePrompt = continuePoint.PagesRead < continuePoint.Pages,
                DaysSinceLastRead = daysSinceLastProgress,
                ChapterOnContinue = reReadChapterDto,
                ChapterOnReread = reReadChapterDto
            };
        }

        var prevChapter = await getPrevChapter();

        // There is no valid previous chapter, use continue point for re-read
        if (prevChapter == null || !isValidPrevChapter(prevChapter))
        {
            var reReadChapterDto = new RereadChapterDto(libraryId, seriesId, continuePoint.VolumeId, continuePoint.Id, continuePointLabel, continuePoint.Format);

            return new RereadDto
            {
                ShouldPrompt = continuePoint.PagesRead >= continuePoint.Pages || reReadForTime,
                TimePrompt = continuePoint.PagesRead < continuePoint.Pages,
                DaysSinceLastRead = daysSinceLastProgress,
                ChapterOnContinue = reReadChapterDto,
                ChapterOnReread = reReadChapterDto,
            };
        }

        // Prompt if it's been a while and might need a refresher (start with the prev chapter)
        var prevChapterLabel = await FormatReReadLabel(userId, namingContext, prevChapter);

        return new RereadDto
        {
            ShouldPrompt = reReadForTime,
            TimePrompt = true,
            DaysSinceLastRead = daysSinceLastProgress,
            ChapterOnContinue = new RereadChapterDto(libraryId, seriesId, continuePoint.VolumeId, continuePoint.Id, continuePointLabel, continuePoint.Format),
            ChapterOnReread = new RereadChapterDto(libraryId, seriesId, prevChapter.VolumeId, prevChapter.Id, prevChapterLabel, prevChapter.Format),
        };
    }

    public async Task<RereadDto> CheckChapterForReRead(int userId, int chapterId, int seriesId, int libraryId)
    {
        var userPreferences = await unitOfWork.UserRepository.GetPreferencesForUser(userId);

        var chapter = await unitOfWork.ChapterRepository.GetChapterDtoAsync(chapterId, userId);
        if (chapter == null) return RereadDto.Dont();

        var lastProgress = await unitOfWork.AppUserProgressRepository.GetLatestProgressForChapter(chapterId, userId);

        var namingContext = await CreateNamingContext(userId, libraryId);

        var chapterLabel = await FormatReReadLabel(userId, namingContext, chapter);
        var reReadChapter = new RereadChapterDto(libraryId, seriesId, chapter.VolumeId, chapterId, chapterLabel, chapter.Format);

        // No progress, read it
        if (lastProgress == null || chapter.PagesRead == 0)
        {
            return new RereadDto
            {
                ShouldPrompt = false,
                ChapterOnContinue = reReadChapter,
            };
        }

        var daysSinceLastProgress = (DateTime.UtcNow - lastProgress.Value).Days;
        var reReadForTime = daysSinceLastProgress != 0 && daysSinceLastProgress > userPreferences.PromptForRereadsAfter;

        // Prompt if fully read or long ago
        return new RereadDto
        {
            ShouldPrompt = chapter.PagesRead >= chapter.Pages || reReadForTime,
            TimePrompt = chapter.PagesRead < chapter.Pages,
            DaysSinceLastRead = daysSinceLastProgress,
            ChapterOnContinue = reReadChapter,
            ChapterOnReread = reReadChapter,
        };
    }

    private async Task<LocalizedNamingContext> CreateNamingContext(int userId, int libraryId)
    {
        var libraryType = await unitOfWork.LibraryRepository.GetLibraryTypeAsync(libraryId);
        return await LocalizedNamingContext.CreateAsync(namingService, localizationService, userId, libraryType);
    }

    private async Task<string> FormatReReadLabel(int userId, LocalizedNamingContext namingContext, ChapterDto chapter)
    {
        if (Parser.IsLooseLeafVolume(chapter.Title))
        {
            var volume = await unitOfWork.VolumeRepository.GetVolumeDtoAsync(chapter.VolumeId, userId);
            if (volume != null)
            {
                var volumeLabel = namingContext.FormatVolumeName(volume);
                if (!string.IsNullOrEmpty(volumeLabel))
                {
                    return volumeLabel;
                }
            }
        }

        return namingContext.FormatChapterTitle(chapter);
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
