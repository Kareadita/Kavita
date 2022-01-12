using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.Entities;
using Microsoft.Extensions.Logging;

namespace API.Services;

public interface IReaderService
{
    void MarkChaptersAsRead(AppUser user, int seriesId, IEnumerable<Chapter> chapters);
    void MarkChaptersAsUnread(AppUser user, int seriesId, IEnumerable<Chapter> chapters);
    Task<bool> SaveReadingProgress(ProgressDto progressDto, int userId);
    Task<int> CapPageToChapter(int chapterId, int page);
    Task<int> GetNextChapterIdAsync(int seriesId, int volumeId, int currentChapterId, int userId);
    Task<int> GetPrevChapterIdAsync(int seriesId, int volumeId, int currentChapterId, int userId);
}

public class ReaderService : IReaderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReaderService> _logger;
    private readonly IDirectoryService _directoryService;
    private readonly ICacheService _cacheService;
    private readonly ChapterSortComparer _chapterSortComparer = new ChapterSortComparer();
    private readonly ChapterSortComparerZeroFirst _chapterSortComparerForInChapterSorting = new ChapterSortComparerZeroFirst();

    public ReaderService(IUnitOfWork unitOfWork, ILogger<ReaderService> logger, IDirectoryService directoryService, ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _directoryService = directoryService;
        _cacheService = cacheService;
    }

    public static string FormatBookmarkFolderPath(string baseDirectory, int userId, int seriesId, int chapterId)
    {
        return Path.Join(baseDirectory, $"{userId}", $"{seriesId}", $"{chapterId}");
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

            if (userProgress == null)
            {
                user.Progresses.Add(new AppUserProgress
                {
                    PagesRead = 0,
                    VolumeId = chapter.VolumeId,
                    SeriesId = seriesId,
                    ChapterId = chapter.Id
                });
            }
            else
            {
                userProgress.PagesRead = 0;
                userProgress.SeriesId = seriesId;
                userProgress.VolumeId = chapter.VolumeId;
            }
        }
    }

    /// <summary>
    /// Gets the User Progress for a given Chapter. This will handle any duplicates that might have occured in past versions and will delete them. Does not commit.
    /// </summary>
    /// <param name="user"></param>
    /// <param name="chapter"></param>
    /// <returns></returns>
    public static AppUserProgress GetUserProgressForChapter(AppUser user, Chapter chapter)
    {
        AppUserProgress userProgress = null;
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
                user.Progresses = new List<AppUserProgress>()
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

            if (await _unitOfWork.CommitAsync())
            {
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
    /// V1 → V2 → V3 chapter 0 → V3 chapter 10 → SP 01 → SP 02
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
            var chapterId = GetNextChapterId(currentVolume.Chapters.OrderBy(x => x.Range, new NaturalSortComparer()), currentChapter.Number);
            if (chapterId > 0) return chapterId;
        }

        foreach (var volume in volumes)
        {
            if (volume.Number == currentVolume.Number && volume.Chapters.Count > 1)
            {
                // Handle Chapters within current Volume
                // In this case, i need 0 first because 0 represents a full volume file.
                var chapterId = GetNextChapterId(currentVolume.Chapters.OrderBy(x => double.Parse(x.Number), _chapterSortComparerForInChapterSorting), currentChapter.Number);
                if (chapterId > 0) return chapterId;
            }

            if (volume.Number != currentVolume.Number + 1) continue;

            // Handle Chapters within next Volume
            // ! When selecting the chapter for the next volume, we need to make sure a c0 comes before a c1+
            var chapters = volume.Chapters.OrderBy(x => double.Parse(x.Number), _chapterSortComparer).ToList();
            if (currentChapter.Number.Equals("0") && chapters.Last().Number.Equals("0"))
            {
                return chapters.Last().Id;
            }

            var firstChapter = chapters.FirstOrDefault();
            if (firstChapter == null) return -1;
            return firstChapter.Id;

        }

        return -1;
    }
    /// <summary>
    /// Tries to find the prev logical Chapter
    /// </summary>
    /// <example>
    /// V1 ← V2 ← V3 chapter 0 ← V3 chapter 10 ← SP 01 ← SP 02
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
            var chapterId = GetNextChapterId(currentVolume.Chapters.OrderBy(x => x.Range, new NaturalSortComparer()).Reverse(), currentChapter.Number);
            if (chapterId > 0) return chapterId;
        }

        foreach (var volume in volumes)
        {
            if (volume.Number == currentVolume.Number)
            {
                var chapterId = GetNextChapterId(currentVolume.Chapters.OrderBy(x => double.Parse(x.Number), _chapterSortComparerForInChapterSorting).Reverse(), currentChapter.Number);
                if (chapterId > 0) return chapterId;
            }
            if (volume.Number == currentVolume.Number - 1)
            {
                var lastChapter = volume.Chapters
                    .OrderBy(x => double.Parse(x.Number), _chapterSortComparerForInChapterSorting).LastOrDefault();
                if (lastChapter == null) return -1;
                return lastChapter.Id;
            }
        }
        return -1;
    }


    private static int GetNextChapterId(IEnumerable<ChapterDto> chapters, string currentChapterNumber)
    {
        var next = false;
        var chaptersList = chapters.ToList();
        foreach (var chapter in chaptersList)
        {
            if (next)
            {
                return chapter.Id;
            }
            if (currentChapterNumber.Equals(chapter.Number)) next = true;
        }

        return -1;
    }


}
