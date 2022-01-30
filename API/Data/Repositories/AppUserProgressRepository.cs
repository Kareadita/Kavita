﻿using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using API.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface IAppUserProgressRepository
{
    void Update(AppUserProgress userProgress);
    Task<int> CleanupAbandonedChapters();
    Task<bool> UserHasProgress(LibraryType libraryType, int userId);
    Task<AppUserProgress> GetUserProgressAsync(int chapterId, int userId);
    Task<bool> HasAnyProgressOnSeriesAsync(int seriesId, int userId);
}

public class AppUserProgressRepository : IAppUserProgressRepository
{
    private readonly DataContext _context;

    public AppUserProgressRepository(DataContext context)
    {
        _context = context;
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

    public async Task<AppUserProgress> GetUserProgressAsync(int chapterId, int userId)
    {
        return await _context.AppUserProgresses
            .Where(p => p.ChapterId == chapterId && p.AppUserId == userId)
            .FirstOrDefaultAsync();
    }
}
