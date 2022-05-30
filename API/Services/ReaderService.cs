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
using API.Extensions;
using API.SignalR;
using Kavita.Common;
using Microsoft.Extensions.Logging;

namespace API.Services;

public interface IReaderService
{
    Task MarkSeriesAsRead(AppUser user, int seriesId);
    Task MarkSeriesAsUnread(AppUser user, int seriesId);
    void MarkChaptersAsRead(AppUser user, int seriesId, IEnumerable<Chapter> chapters);
    void MarkChaptersAsUnread(AppUser user, int seriesId, IEnumerable<Chapter> chapters);
    Task<bool> SaveReadingProgress(ProgressDto progressDto, int userId);
    Task<int> CapPageToChapter(int chapterId, int page);
    Task<int> GetNextChapterIdAsync(int seriesId, int volumeId, int currentChapterId, int userId);
    Task<int> GetPrevChapterIdAsync(int seriesId, int volumeId, int currentChapterId, int userId);
    Task<ChapterDto> GetContinuePoint(int seriesId, int userId);
    Task MarkChaptersUntilAsRead(AppUser user, int seriesId, float chapterNumber);
    Task MarkVolumesUntilAsRead(AppUser user, int seriesId, int volumeNumber);
    HourEstimateRangeDto GetTimeEstimate(long wordCount, int pageCount, bool isEpub, bool hasProgress = false);
}

public class ReaderService : IReaderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReaderService> _logger;
    private readonly IEventHub _eventHub;
    private readonly ChapterSortComparer _chapterSortComparer = new ChapterSortComparer();
    private readonly ChapterSortComparerZeroFirst _chapterSortComparerForInChapterSorting = new ChapterSortComparerZeroFirst();

    public const float MinWordsPerHour = 10260F;
    public const float MaxWordsPerHour = 30000F;
    public const float AvgWordsPerHour = (MaxWordsPerHour + MinWordsPerHour) / 2F;
    public const float MinPagesPerMinute = 3.33F;
    public const float MaxPagesPerMinute = 2.75F;
    public const float AvgPagesPerMinute = (MaxPagesPerMinute + MinPagesPerMinute) / 2F;


    public ReaderService(IUnitOfWork unitOfWork, ILogger<ReaderService> logger, IEventHub eventHub)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _eventHub = eventHub;
    }

    public static string FormatBookmarkFolderPath(string baseDirectory, int userId, int seriesId, int chapterId)
    {
        return Parser.Parser.NormalizePath(Path.Join(baseDirectory, $"{userId}", $"{seriesId}", $"{chapterId}"));
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
            MarkChaptersAsRead(user, seriesId, volume.Chapters);
        }

        _unitOfWork.UserRepository.Update(user);
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
            MarkChaptersAsUnread(user, seriesId, volume.Chapters);
        }

        _unitOfWork.UserRepository.Update(user);
    }

    /// <summary>
    /// Marks all Chapters as Read by creating or updating UserProgress rows. Does not commit.
    /// </summary>
    /// <param name="user"></param>
    /// <param name="seriesId"></param>
    /// <param name="chapters"></param>
    public void MarkChaptersAsRead(AppUser user, int seriesId, IEnumerable<Chapter> chapters)
    {
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
                    ChapterId = chapter.Id
                });
            }
            else
            {
                userProgress.PagesRead = chapter.Pages;
                userProgress.SeriesId = seriesId;
                userProgress.VolumeId = chapter.VolumeId;
            }
        }
    }

    /// <summary>
    /// Marks all Chapters as Unread by creating or updating UserProgress rows. Does not commit.
    /// </summary>
    /// <param name="user"></param>
    /// <param name="seriesId"></param>
    /// <param name="chapters"></param>
    public void MarkChaptersAsUnread(AppUser user, int seriesId, IEnumerable<Chapter> chapters)
    {
        foreach (var chapter in chapters)
        {
            var userProgress = GetUserProgressForChapter(user, chapter);

            if (userProgress == null) continue;

            userProgress.PagesRead = 0;
            userProgress.SeriesId = seriesId;
            userProgress.VolumeId = chapter.VolumeId;
        }
    }

    /// <summary>
    /// Gets the User Progress for a given Chapter. This will handle any duplicates that might have occured in past versions and will delete them. Does not commit.
    /// </summary>
    /// <param name="user">Must have Progresses populated</param>
    /// <param name="chapter"></param>
    /// <returns></returns>
    private static AppUserProgress GetUserProgressForChapter(AppUser user, Chapter chapter)
    {
        AppUserProgress userProgress = null;

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
            var userProgress =
                await _unitOfWork.AppUserProgressRepository.GetUserProgressAsync(progressDto.ChapterId, userId);

            if (userProgress == null)
            {
                // Create a user object
                var userWithProgress =
                    await _unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.Progress);
                userWithProgress.Progresses ??= new List<AppUserProgress>();
                userWithProgress.Progresses.Add(new AppUserProgress
                {
                    PagesRead = progressDto.PageNum,
                    VolumeId = progressDto.VolumeId,
                    SeriesId = progressDto.SeriesId,
                    ChapterId = progressDto.ChapterId,
                    BookScrollId = progressDto.BookScrollId,
                    LastModified = DateTime.Now
                });
                _unitOfWork.UserRepository.Update(userWithProgress);
            }
            else
            {
                userProgress.PagesRead = progressDto.PageNum;
                userProgress.SeriesId = progressDto.SeriesId;
                userProgress.VolumeId = progressDto.VolumeId;
                userProgress.BookScrollId = progressDto.BookScrollId;
                userProgress.LastModified = DateTime.Now;
                _unitOfWork.AppUserProgressRepository.Update(userProgress);
            }

            if (!_unitOfWork.HasChanges() || await _unitOfWork.CommitAsync())
            {
                var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
                await _eventHub.SendMessageAsync(MessageFactory.UserProgressUpdate,
                    MessageFactory.UserProgressUpdateEvent(userId, user.UserName, progressDto.SeriesId, progressDto.VolumeId, progressDto.ChapterId, progressDto.PageNum));
                return true;
            }
        }
        catch (Exception exception)
        {
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
        var totalPages = await _unitOfWork.ChapterRepository.GetChapterTotalPagesAsync(chapterId);
        if (page > totalPages)
        {
            page = totalPages;
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

        foreach (var volume in volumes)
        {
            if (volume.Number == currentVolume.Number && volume.Chapters.Count > 1)
            {
                // Handle Chapters within current Volume
                // In this case, i need 0 first because 0 represents a full volume file.
                var chapterId = GetNextChapterId(currentVolume.Chapters.OrderBy(x => double.Parse(x.Number), _chapterSortComparer),
                    currentChapter.Range, dto => dto.Range);
                if (chapterId > 0) return chapterId;

            }

            if (volume.Number != currentVolume.Number + 1) continue;

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
        }

        // If we are the last volume and we didn't find any next volume, loop back to volume 0 and give the first chapter
        // This has an added problem that it will loop up to the beginning always
        // Should I change this to Max number? volumes.LastOrDefault()?.Number -> volumes.Max(v => v.Number)
        if (currentVolume.Number != 0 && currentVolume.Number == volumes.LastOrDefault()?.Number && volumes.Count > 1)
        {
            var chapterVolume = volumes.FirstOrDefault();
            if (chapterVolume?.Number != 0) return -1;
            var firstChapter = chapterVolume.Chapters.OrderBy(x => double.Parse(x.Number), _chapterSortComparer).FirstOrDefault();
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

        foreach (var volume in volumes)
        {
            if (volume.Number == currentVolume.Number)
            {
                var chapterId = GetNextChapterId(currentVolume.Chapters.OrderBy(x => double.Parse(x.Number), _chapterSortComparerForInChapterSorting).Reverse(),
                    currentChapter.Range, dto => dto.Range);
                if (chapterId > 0) return chapterId;
            }
            if (volume.Number == currentVolume.Number - 1)
            {
                if (currentVolume.Number - 1 == 0) break; // If we have walked all the way to chapter volume, then we should break so logic outside can work
                var lastChapter = volume.Chapters
                    .OrderBy(x => double.Parse(x.Number), _chapterSortComparerForInChapterSorting).LastOrDefault();
                if (lastChapter == null) return -1;
                return lastChapter.Id;
            }
        }

        var lastVolume = volumes.OrderBy(v => v.Number).LastOrDefault();
        if (currentVolume.Number == 0 && currentVolume.Number != lastVolume?.Number && lastVolume?.Chapters.Count > 1)
        {
            var lastChapter = lastVolume.Chapters.OrderBy(x => double.Parse(x.Number), _chapterSortComparerForInChapterSorting).LastOrDefault();
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
             return volumes.OrderBy(v => double.Parse(v.Number + ""), _chapterSortComparer).First().Chapters
                 .OrderBy(c => float.Parse(c.Number)).First();
         }

        // Loop through all chapters that are not in volume 0
        var volumeChapters = volumes
            .Where(v => v.Number != 0)
            .SelectMany(v => v.Chapters)
            .OrderBy(c => float.Parse(c.Number))
            .ToList();

        // If there are any volumes that have progress, return those. If not, move on.
        var currentlyReadingChapter = volumeChapters.FirstOrDefault(chapter => chapter.PagesRead < chapter.Pages);
        if (currentlyReadingChapter != null) return currentlyReadingChapter;

        // Order with volume 0 last so we prefer the natural order
        return FindNextReadingChapter(volumes.OrderBy(v => v.Number, new SortComparerZeroLast()).SelectMany(v => v.Chapters).ToList());
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
            return chaptersWithProgress.ElementAt(last);
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
                .Where(c => !c.IsSpecial && Parser.Parser.MaxNumberFromRange(c.Range) <= chapterNumber);
            MarkChaptersAsRead(user, volume.SeriesId, chapters);
        }
    }

    public async Task MarkVolumesUntilAsRead(AppUser user, int seriesId, int volumeNumber)
    {
        var volumes = await _unitOfWork.VolumeRepository.GetVolumesForSeriesAsync(new List<int> { seriesId }, true);
        foreach (var volume in volumes.OrderBy(v => v.Number).Where(v => v.Number <= volumeNumber && v.Number > 0))
        {
            MarkChaptersAsRead(user, volume.SeriesId, volume.Chapters);
        }
    }

    public HourEstimateRangeDto GetTimeEstimate(long wordCount, int pageCount, bool isEpub, bool hasProgress = false)
    {
        if (isEpub)
        {
            return new HourEstimateRangeDto
            {
                MinHours = Math.Max((int) Math.Round((wordCount / MinWordsPerHour)), 1),
                MaxHours = Math.Max((int) Math.Round((wordCount / MaxWordsPerHour)), 1),
                AvgHours = (int) Math.Round((wordCount / AvgWordsPerHour)),
                HasProgress = hasProgress
            };
        }

        return new HourEstimateRangeDto
        {
            MinHours = Math.Max((int) Math.Round((pageCount / MinPagesPerMinute / 60F)), 1),
            MaxHours = Math.Max((int) Math.Round((pageCount / MaxPagesPerMinute / 60F)), 1),
            AvgHours = (int) Math.Round((pageCount / AvgPagesPerMinute / 60F)),
            HasProgress = hasProgress
        };
    }
}
