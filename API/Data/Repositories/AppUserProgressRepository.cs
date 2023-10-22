using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data.ManualMigrations;
using API.DTOs;
using API.Entities;
using API.Entities.Enums;
using API.Services.Tasks.Scanner.Parser;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;
#nullable enable
public interface IAppUserProgressRepository
{
    void Update(AppUserProgress userProgress);
    Task<int> CleanupAbandonedChapters();
    Task<bool> UserHasProgress(LibraryType libraryType, int userId);
    Task<AppUserProgress?> GetUserProgressAsync(int chapterId, int userId);
    Task<bool> HasAnyProgressOnSeriesAsync(int seriesId, int userId);
    /// <summary>
    /// This is built exclusively for <see cref="MigrateUserProgressLibraryId"/>
    /// </summary>
    /// <returns></returns>
    Task<AppUserProgress?> GetAnyProgress();
    Task<IEnumerable<AppUserProgress>> GetUserProgressForSeriesAsync(int seriesId, int userId);
    Task<IEnumerable<AppUserProgress>> GetAllProgress();
    Task<DateTime> GetLatestProgress();
    Task<ProgressDto?> GetUserProgressDtoAsync(int chapterId, int userId);
    Task<bool> AnyUserProgressForSeriesAsync(int seriesId, int userId);
    Task<int> GetHighestFullyReadChapterForSeries(int seriesId, int userId);
    Task<int> GetHighestFullyReadVolumeForSeries(int seriesId, int userId);
    Task<DateTime?> GetLatestProgressForSeries(int seriesId, int userId);
    Task<DateTime?> GetFirstProgressForSeries(int seriesId, int userId);
}
#nullable disable
public class AppUserProgressRepository : IAppUserProgressRepository
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;

    public AppUserProgressRepository(DataContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public void Update(AppUserProgress userProgress)
    {
        _context.Entry(userProgress).State = EntityState.Modified;
    }

    /// <summary>
    /// This will remove any entries that have chapterIds that no longer exists. This will execute the save as well.
    /// </summary>
    public async Task<int> CleanupAbandonedChapters()
    {
        var chapterIds = _context.Chapter.Select(c => c.Id);

        var rowsToRemove = await _context.AppUserProgresses
            .Where(progress => !chapterIds.Contains(progress.ChapterId))
            .ToListAsync();

        var rowsToRemoveBookmarks = await _context.AppUserBookmark
            .Where(progress => !chapterIds.Contains(progress.ChapterId))
            .ToListAsync();

        var rowsToRemoveReadingLists = await _context.ReadingListItem
            .Where(item => !chapterIds.Contains(item.ChapterId))
            .ToListAsync();

        _context.RemoveRange(rowsToRemove);
        _context.RemoveRange(rowsToRemoveBookmarks);
        _context.RemoveRange(rowsToRemoveReadingLists);
        return await _context.SaveChangesAsync() > 0 ? rowsToRemove.Count : 0;
    }

    /// <summary>
    /// Checks if user has any progress against a library of passed type
    /// </summary>
    /// <param name="libraryType"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<bool> UserHasProgress(LibraryType libraryType, int userId)
    {
        var seriesIds = await _context.AppUserProgresses
            .Where(aup => aup.PagesRead > 0 && aup.AppUserId == userId)
            .AsNoTracking()
            .Select(aup => aup.SeriesId)
            .ToListAsync();

        if (seriesIds.Count == 0) return false;

        return await _context.Series
            .Include(s => s.Library)
            .Where(s => seriesIds.Contains(s.Id) && s.Library.Type == libraryType)
            .AsNoTracking()
            .AnyAsync();
    }

    public async Task<bool> HasAnyProgressOnSeriesAsync(int seriesId, int userId)
    {
        return await _context.AppUserProgresses
            .AnyAsync(aup => aup.PagesRead > 0 && aup.AppUserId == userId && aup.SeriesId == seriesId);
    }

    #nullable enable
    public async Task<AppUserProgress?> GetAnyProgress()
    {
        return await _context.AppUserProgresses.FirstOrDefaultAsync();
    }
    #nullable disable

    /// <summary>
    /// This will return any user progress. This filters out progress rows that have no pages read.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<IEnumerable<AppUserProgress>> GetUserProgressForSeriesAsync(int seriesId, int userId)
    {
        return await _context.AppUserProgresses
            .Where(p => p.SeriesId == seriesId && p.AppUserId == userId && p.PagesRead > 0)
            .ToListAsync();
    }

    public async Task<IEnumerable<AppUserProgress>> GetAllProgress()
    {
        return await _context.AppUserProgresses.ToListAsync();
    }

    /// <summary>
    /// Returns the latest progress in UTC
    /// </summary>
    /// <returns></returns>
    public async Task<DateTime> GetLatestProgress()
    {
        return await _context.AppUserProgresses
            .Select(d => d.LastModifiedUtc)
            .OrderByDescending(d => d)
            .FirstOrDefaultAsync();
    }

    public async Task<ProgressDto?> GetUserProgressDtoAsync(int chapterId, int userId)
    {
        return await _context.AppUserProgresses
            .Where(p => p.AppUserId == userId && p.ChapterId == chapterId)
            .ProjectTo<ProgressDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> AnyUserProgressForSeriesAsync(int seriesId, int userId)
    {
        return await _context.AppUserProgresses
            .Where(p => p.SeriesId == seriesId && p.AppUserId == userId && p.PagesRead > 0)
            .AnyAsync();
    }

    public async Task<int> GetHighestFullyReadChapterForSeries(int seriesId, int userId)
    {
        var list = await _context.AppUserProgresses
            .Join(_context.Chapter, appUserProgresses => appUserProgresses.ChapterId, chapter => chapter.Id,
                (appUserProgresses, chapter) => new {appUserProgresses, chapter})
            .Where(p => p.appUserProgresses.SeriesId == seriesId && p.appUserProgresses.AppUserId == userId &&
                        p.appUserProgresses.PagesRead >= p.chapter.Pages)
            .Select(p => p.chapter.Range)
            .ToListAsync();
        return list.Count == 0 ? 0 : list.DefaultIfEmpty().Where(d => d != null).Max(d => (int) Math.Floor(Parser.MaxNumberFromRange(d)));
    }

    public async Task<int> GetHighestFullyReadVolumeForSeries(int seriesId, int userId)
    {
        var list = await _context.AppUserProgresses
            .Join(_context.Chapter, appUserProgresses => appUserProgresses.ChapterId, chapter => chapter.Id,
                (appUserProgresses, chapter) => new {appUserProgresses, chapter})
            .Where(p => p.appUserProgresses.SeriesId == seriesId && p.appUserProgresses.AppUserId == userId &&
                        p.appUserProgresses.PagesRead >= p.chapter.Pages)
            .Select(p => p.chapter.Volume.Number)
            .ToListAsync();
        return list.Count == 0 ? 0 : list.DefaultIfEmpty().Max();
    }

    public async Task<DateTime?> GetLatestProgressForSeries(int seriesId, int userId)
    {
        var list = await _context.AppUserProgresses.Where(p => p.AppUserId == userId && p.SeriesId == seriesId)
            .Select(p => p.LastModifiedUtc)
            .ToListAsync();
        return list.Count == 0 ? null : list.DefaultIfEmpty().Max();
    }

    public async Task<DateTime?> GetFirstProgressForSeries(int seriesId, int userId)
    {
        var list = await _context.AppUserProgresses.Where(p => p.AppUserId == userId && p.SeriesId == seriesId)
            .Select(p => p.LastModifiedUtc)
            .ToListAsync();
        return list.Count == 0 ? null : list.DefaultIfEmpty().Min();
    }

#nullable enable
    public async Task<AppUserProgress?> GetUserProgressAsync(int chapterId, int userId)
    {
        return await _context.AppUserProgresses
            .Where(p => p.ChapterId == chapterId && p.AppUserId == userId)
            .FirstOrDefaultAsync();
    }
}
