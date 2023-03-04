﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.Reader;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.SignalR;
using Kavita.Common;
using Microsoft.Extensions.Logging;

namespace API.Services;

public interface IReaderService
{
    Task MarkSeriesAsRead(AppUser user, int seriesId);
    Task MarkSeriesAsUnread(AppUser user, int seriesId);
    Task MarkChaptersAsRead(AppUser user, int seriesId, IList<Chapter> chapters);
    Task MarkChaptersAsUnread(AppUser user, int seriesId, IList<Chapter> chapters);
    Task<bool> SaveReadingProgress(ProgressDto progressDto, int userId);
    Task<int> CapPageToChapter(int chapterId, int page);
    int CapPageToChapter(Chapter chapter, int page);
    Task<int> GetNextChapterIdAsync(int seriesId, int volumeId, int currentChapterId, int userId);
    Task<int> GetPrevChapterIdAsync(int seriesId, int volumeId, int currentChapterId, int userId);
    Task<ChapterDto> GetContinuePoint(int seriesId, int userId);
    Task MarkChaptersUntilAsRead(AppUser user, int seriesId, float chapterNumber);
    Task MarkVolumesUntilAsRead(AppUser user, int seriesId, int volumeNumber);
    HourEstimateRangeDto GetTimeEstimate(long wordCount, int pageCount, bool isEpub);
    IDictionary<int, int> GetPairs(IEnumerable<FileDimensionDto> dimensions);
}

public class ReaderService : IReaderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReaderService> _logger;
    private readonly IEventHub _eventHub;
    private readonly ChapterSortComparer _chapterSortComparer = ChapterSortComparer.Default;
    private readonly ChapterSortComparerZeroFirst _chapterSortComparerForInChapterSorting = ChapterSortComparerZeroFirst.Default;

    private const float MinWordsPerHour = 10260F;
    private const float MaxWordsPerHour = 30000F;
    public const float AvgWordsPerHour = (MaxWordsPerHour + MinWordsPerHour) / 2F;
    private const float MinPagesPerMinute = 3.33F;
    private const float MaxPagesPerMinute = 2.75F;
    public const float AvgPagesPerMinute = (MaxPagesPerMinute + MinPagesPerMinute) / 2F;


    public ReaderService(IUnitOfWork unitOfWork, ILogger<ReaderService> logger, IEventHub eventHub)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _eventHub = eventHub;
    }

    public static string FormatBookmarkFolderPath(string baseDirectory, int userId, int seriesId, int chapterId)
    {
        return Tasks.Scanner.Parser.Parser.NormalizePath(Path.Join(baseDirectory, $"{userId}", $"{seriesId}", $"{chapterId}"));
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
        if (series == null) throw new KavitaException("Series suddenly doesn't exist, cannot mark as read");
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
                    LibraryId = series.LibraryId
                });
            }
            else
            {
                userProgress.PagesRead = chapter.Pages;
                userProgress.SeriesId = seriesId;
                userProgress.VolumeId = chapter.VolumeId;
            }

            await _eventHub.SendMessageAsync(MessageFactory.UserProgressUpdate,
                MessageFactory.UserProgressUpdateEvent(user.Id, user.UserName!, seriesId, chapter.VolumeId, chapter.Id, chapter.Pages));

            // Send out volume events for each distinct volume
            if (!seenVolume.ContainsKey(chapter.VolumeId))
            {
                seenVolume[chapter.VolumeId] = true;
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
    private static AppUserProgress? GetUserProgressForChapter(AppUser user, Chapter chapter)
    {
        AppUserProgress? userProgress = null;

        if (user.Progresses == null)
        {
            throw new KavitaException("Progresses must exist on user");
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
                user.Progresses = new List<AppUserProgress>
                {
                    user.Progresses.First()
                };
                userProgress = user.Progresses.First();
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
        progressDto.PageNum = await CapPageToChapter(progressDto.ChapterId, progressDto.PageNum);

        try
        {
            // TODO: Rewrite this code to just pull user object with progress for that particular appuserprogress, else create it
            var userProgress =
                await _unitOfWork.AppUserProgressRepository.GetUserProgressAsync(progressDto.ChapterId, userId);


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
                    BookScrollId = progressDto.BookScrollId
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

            if (!_unitOfWork.HasChanges() || await _unitOfWork.CommitAsync())
            {
                var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
                await _eventHub.SendMessageAsync(MessageFactory.UserProgressUpdate,
                    MessageFactory.UserProgressUpdateEvent(userId, user!.UserName!, progressDto.SeriesId, progressDto.VolumeId, progressDto.ChapterId, progressDto.PageNum));
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
    public async Task<int> CapPageToChapter(int chapterId, int page)
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

        return page;
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

    /// <summary>
    /// Tries to find the next logical Chapter
    /// </summary>
    /// <example>
    /// V1 → V2 → V3 chapter 0 → V3 chapter 10 → V0 chapter 1 -> V0 chapter 2 -> SP 01 → SP 02
    /// </example>
    /// <param name="seriesId"></param>
    /// <param name="volumeId"></param>
    /// <param name="currentChapterId"></param>
    /// <param name="userId"></param>
    /// <returns>-1 if nothing can be found</returns>
    public async Task<int> GetNextChapterIdAsync(int seriesId, int volumeId, int currentChapterId, int userId)
    {
        var volumes = (await _unitOfWork.VolumeRepository.GetVolumesDtoAsync(seriesId, userId)).ToList();
        var currentVolume = volumes.Single(v => v.Id == volumeId);
        var currentChapter = currentVolume.Chapters.Single(c => c.Id == currentChapterId);

        if (currentVolume.Number == 0)
        {
            // Handle specials by sorting on their Filename aka Range
            var chapterId = GetNextChapterId(currentVolume.Chapters.OrderByNatural(x => x.Range), currentChapter.Range, dto => dto.Range);
            if (chapterId > 0) return chapterId;
        }

        var currentVolumeNumber = float.Parse(currentVolume.Name);
        var next = false;
        foreach (var volume in volumes)
        {
            var volumeNumbersMatch = Math.Abs(float.Parse(volume.Name) - currentVolumeNumber) < 0.00001f;
            if (volumeNumbersMatch && volume.Chapters.Count > 1)
            {
                // Handle Chapters within current Volume
                // In this case, i need 0 first because 0 represents a full volume file.
                var chapterId = GetNextChapterId(currentVolume.Chapters.OrderBy(x => double.Parse(x.Number), _chapterSortComparer),
                    currentChapter.Range, dto => dto.Range);
                if (chapterId > 0) return chapterId;
                next = true;
                continue;
            }

            if (volumeNumbersMatch)
            {
                next = true;
                continue;
            }

            if (!next) continue;

            // Handle Chapters within next Volume
            // ! When selecting the chapter for the next volume, we need to make sure a c0 comes before a c1+
            var chapters = volume.Chapters.OrderBy(x => double.Parse(x.Number), _chapterSortComparer).ToList();
            if (currentChapter.Number.Equals("0") && chapters.Last().Number.Equals("0"))
            {
                // We need to handle an extra check if the current chapter is the last special, as we should return -1
                if (currentChapter.IsSpecial) return -1;

                return chapters.Last().Id;
            }

            var firstChapter = chapters.FirstOrDefault();
            if (firstChapter == null) break;
            var isSpecial = firstChapter.IsSpecial || currentChapter.IsSpecial;
            if (isSpecial)
            {
                var chapterId = GetNextChapterId(volume.Chapters.OrderByNatural(x => x.Number),
                    currentChapter.Range, dto => dto.Range);
                if (chapterId > 0) return chapterId;
            } else if (double.Parse(firstChapter.Number) > double.Parse(currentChapter.Number)) return firstChapter.Id;
            // If we are the last chapter and next volume is there, we should try to use it (unless it's volume 0)
            else if (double.Parse(firstChapter.Number) == 0) return firstChapter.Id;
        }

        // If we are the last volume and we didn't find any next volume, loop back to volume 0 and give the first chapter
        // This has an added problem that it will loop up to the beginning always
        // Should I change this to Max number? volumes.LastOrDefault()?.Number -> volumes.Max(v => v.Number)
        if (currentVolume.Number != 0 && currentVolume.Number == volumes.LastOrDefault()?.Number && volumes.Count > 1)
        {
            var chapterVolume = volumes.FirstOrDefault();
            if (chapterVolume?.Number != 0) return -1;
            var firstChapter = chapterVolume.Chapters.MinBy(x => double.Parse(x.Number), _chapterSortComparer);
            if (firstChapter == null) return -1;
            return firstChapter.Id;
        }

        return -1;
    }
    /// <summary>
    /// Tries to find the prev logical Chapter
    /// </summary>
    /// <example>
    /// V1 ← V2 ← V3 chapter 0 ← V3 chapter 10 ← V0 chapter 1 ← V0 chapter 2 ← SP 01 ← SP 02
    /// </example>
    /// <param name="seriesId"></param>
    /// <param name="volumeId"></param>
    /// <param name="currentChapterId"></param>
    /// <param name="userId"></param>
    /// <returns>-1 if nothing can be found</returns>
    public async Task<int> GetPrevChapterIdAsync(int seriesId, int volumeId, int currentChapterId, int userId)
    {
        var volumes = (await _unitOfWork.VolumeRepository.GetVolumesDtoAsync(seriesId, userId)).Reverse().ToList();
        var currentVolume = volumes.Single(v => v.Id == volumeId);
        var currentChapter = currentVolume.Chapters.Single(c => c.Id == currentChapterId);

        if (currentVolume.Number == 0)
        {
            var chapterId = GetNextChapterId(currentVolume.Chapters.OrderByNatural(x => x.Range).Reverse(), currentChapter.Range,
                dto => dto.Range);
            if (chapterId > 0) return chapterId;
        }

        var next = false;
        foreach (var volume in volumes)
        {
            if (volume.Number == currentVolume.Number)
            {
                var chapterId = GetNextChapterId(currentVolume.Chapters.OrderBy(x => double.Parse(x.Number), _chapterSortComparerForInChapterSorting).Reverse(),
                    currentChapter.Range, dto => dto.Range);
                if (chapterId > 0) return chapterId;
                next = true; // When the diff between volumes is more than 1, we need to explicitly tell that next volume is our use case
                continue;
            }
            if (next)
            {
                if (currentVolume.Number - 1 == 0) break; // If we have walked all the way to chapter volume, then we should break so logic outside can work
                var lastChapter = volume.Chapters.MaxBy(x => double.Parse(x.Number), _chapterSortComparerForInChapterSorting);
                if (lastChapter == null) return -1;
                return lastChapter.Id;
            }
        }

        var lastVolume = volumes.MaxBy(v => v.Number);
        if (currentVolume.Number == 0 && currentVolume.Number != lastVolume?.Number && lastVolume?.Chapters.Count > 1)
        {
            var lastChapter = lastVolume.Chapters.MaxBy(x => double.Parse(x.Number), _chapterSortComparerForInChapterSorting);
            if (lastChapter == null) return -1;
            return lastChapter.Id;
        }


        return -1;
    }

    /// <summary>
    /// Finds the chapter to continue reading from. If a chapter has progress and not complete, return that. If not, progress in the
    /// ordering (Volumes -> Loose Chapters -> Special) to find next chapter. If all are read, return first in order for series.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<ChapterDto> GetContinuePoint(int seriesId, int userId)
    {
        var progress = (await _unitOfWork.AppUserProgressRepository.GetUserProgressForSeriesAsync(seriesId, userId)).ToList();
        var volumes = (await _unitOfWork.VolumeRepository.GetVolumesDtoAsync(seriesId, userId)).ToList();

         if (progress.Count == 0)
         {
             // I think i need a way to sort volumes last
             return volumes.OrderBy(v => double.Parse(v.Number + string.Empty), _chapterSortComparer).First().Chapters
                 .OrderBy(c => float.Parse(c.Number)).First();
         }

        // Loop through all chapters that are not in volume 0
        var volumeChapters = volumes
            .Where(v => v.Number != 0)
            .SelectMany(v => v.Chapters)
            .ToList();

        // NOTE: If volume 1 has chapter 1 and volume 2 is just chapter 0 due to being a full volume file, then this fails
        // If there are any volumes that have progress, return those. If not, move on.
        var currentlyReadingChapter = volumeChapters
            .OrderBy(c => double.Parse(c.Range), _chapterSortComparer)
            .FirstOrDefault(chapter => chapter.PagesRead < chapter.Pages && chapter.PagesRead > 0);
        if (currentlyReadingChapter != null) return currentlyReadingChapter;

        // Order with volume 0 last so we prefer the natural order
        return FindNextReadingChapter(volumes.OrderBy(v => v.Number, SortComparerZeroLast.Default).SelectMany(v => v.Chapters).ToList());
    }

    private static ChapterDto FindNextReadingChapter(IList<ChapterDto> volumeChapters)
    {
        var chaptersWithProgress = volumeChapters.Where(c => c.PagesRead > 0).ToList();
        if (chaptersWithProgress.Count <= 0) return volumeChapters.First();


        var last = chaptersWithProgress.FindLastIndex(c => c.PagesRead > 0);
        if (last + 1 < chaptersWithProgress.Count)
        {
            return chaptersWithProgress.ElementAt(last + 1);
        }

        var lastChapter = chaptersWithProgress.ElementAt(last);
        if (lastChapter.PagesRead < lastChapter.Pages)
        {
            return lastChapter;
        }

        // If the last chapter didn't fit, then we need the next chapter without any progress
        var firstChapterWithoutProgress = volumeChapters.FirstOrDefault(c => c.PagesRead == 0);
        if (firstChapterWithoutProgress != null)
        {
            return firstChapterWithoutProgress;
        }

        // chaptersWithProgress are all read, then we need to get the next chapter that doesn't have progress
        var lastIndexWithProgress = volumeChapters.IndexOf(lastChapter);
        if (lastIndexWithProgress + 1 < volumeChapters.Count)
        {
            return volumeChapters.ElementAt(lastIndexWithProgress + 1);
        }

        return volumeChapters.First();
    }


    private static int GetNextChapterId(IEnumerable<ChapterDto> chapters, string currentChapterNumber, Func<ChapterDto, string> accessor)
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
        foreach (var volume in volumes.OrderBy(v => v.Number))
        {
            var chapters = volume.Chapters
                .OrderBy(c => float.Parse(c.Number))
                .Where(c => !c.IsSpecial && Tasks.Scanner.Parser.Parser.MaxNumberFromRange(c.Range) <= chapterNumber);
            await MarkChaptersAsRead(user, volume.SeriesId, chapters.ToList());
        }
    }

    public async Task MarkVolumesUntilAsRead(AppUser user, int seriesId, int volumeNumber)
    {
        var volumes = await _unitOfWork.VolumeRepository.GetVolumesForSeriesAsync(new List<int> { seriesId }, true);
        foreach (var volume in volumes.OrderBy(v => v.Number).Where(v => v.Number <= volumeNumber && v.Number > 0))
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
                AvgHours = (int) Math.Round((wordCount / AvgWordsPerHour))
            };
        }

        var minHoursPages = Math.Max((int) Math.Round((pageCount / MinPagesPerMinute / 60F)), 0);
        var maxHoursPages = Math.Max((int) Math.Round((pageCount / MaxPagesPerMinute / 60F)), 0);
        return new HourEstimateRangeDto
        {
            MinHours = Math.Min(minHoursPages, maxHoursPages),
            MaxHours = Math.Max(minHoursPages, maxHoursPages),
            AvgHours = (int) Math.Round((pageCount / AvgPagesPerMinute / 60F))
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
            case LibraryType.Manga:
                return "Chapter" + (includeSpace ? " " : string.Empty);
            case LibraryType.Comic:
                if (includeHash) {
                    return "Issue #";
                }
                return "Issue" + (includeSpace ? " " : string.Empty);
            case LibraryType.Book:
                return "Book" + (includeSpace ? " " : string.Empty);
            default:
                throw new ArgumentOutOfRangeException(nameof(libraryType), libraryType, null);
        }
    }
}
