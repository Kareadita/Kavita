using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Kavita.API.Repositories;
using Kavita.Common.Constants;
using Kavita.Database.Extensions;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.Progress;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Progress;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Repositories;

public class AppUserProgressRepository(DataContext context, IMapper mapper) : IAppUserProgressRepository
{
    public void Update(AppUserProgress userProgress)
    {
        context.Entry(userProgress).State = EntityState.Modified;
    }

    public void Remove(AppUserProgress userProgress)
    {
        context.Remove(userProgress);
    }

    /// <summary>
    /// This will remove any entries that have chapterIds that no longer exists. This will execute the save as well.
    /// </summary>
    /// <param name="ct"></param>
    public async Task<int> CleanupAbandonedChapters(CancellationToken ct = default)
    {
        var chapterIds = context.Chapter.Select(c => c.Id);

        var rowsToRemove = await context.AppUserProgresses
            .Where(progress => !chapterIds.Contains(progress.ChapterId))
            .ToListAsync(ct);

        var rowsToRemoveBookmarks = await context.AppUserBookmark
            .Where(progress => !chapterIds.Contains(progress.ChapterId))
            .ToListAsync(ct);

        var rowsToRemoveReadingLists = await context.ReadingListItem
            .Where(item => !chapterIds.Contains(item.ChapterId))
            .ToListAsync(ct);

        context.RemoveRange(rowsToRemove);
        context.RemoveRange(rowsToRemoveBookmarks);
        context.RemoveRange(rowsToRemoveReadingLists);
        return await context.SaveChangesAsync(ct) > 0 ? rowsToRemove.Count : 0;
    }

    /// <summary>
    /// Checks if a user has any progress against a library of a passed type
    /// </summary>
    /// <param name="libraryType"></param>
    /// <param name="userId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<bool> UserHasProgress(LibraryType libraryType, int userId, CancellationToken ct = default)
    {
        var seriesIds = await context.AppUserProgresses
            .Where(aup => aup.PagesRead > 0 && aup.AppUserId == userId)
            .AsNoTracking()
            .Select(aup => aup.SeriesId)
            .ToListAsync(ct);

        if (seriesIds.Count == 0) return false;

        return await context.Series
            .Include(s => s.Library)
            .Where(s => seriesIds.Contains(s.Id) && s.Library.Type == libraryType)
            .AsNoTracking()
            .AnyAsync(ct);
    }

    public async Task<bool> HasAnyProgressOnSeriesAsync(int seriesId, int userId, CancellationToken ct = default)
    {
        return await context.AppUserProgresses
            .AnyAsync(aup => aup.PagesRead > 0 && aup.AppUserId == userId && aup.SeriesId == seriesId, ct);
    }

    /// <summary>
    /// This will return any user progress. This filters out progress rows that have no pages read.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="userId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<IEnumerable<AppUserProgress>> GetUserProgressForSeriesAsync(int seriesId, int userId,
        CancellationToken ct = default)
    {
        return await context.AppUserProgresses
            .Where(p => p.SeriesId == seriesId && p.AppUserId == userId && p.PagesRead > 0)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<AppUserProgress>> GetAllProgress(CancellationToken ct = default)
    {
        return await context.AppUserProgresses.ToListAsync(ct);
    }

    /// <summary>
    /// Returns the latest progress in UTC
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<DateTime> GetLatestProgress(CancellationToken ct = default)
    {
        return await context.AppUserProgresses
            .Select(d => d.LastModifiedUtc)
            .OrderByDescending(d => d)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ProgressDto?> GetUserProgressDtoAsync(int chapterId, int userId, CancellationToken ct = default)
    {
        return await context.AppUserProgresses
            .Where(p => p.AppUserId == userId && p.ChapterId == chapterId)
            .ProjectTo<ProgressDto>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> AnyUserProgressForSeriesAsync(int seriesId, int userId, CancellationToken ct = default)
    {
        return await context.AppUserProgresses
            .Where(p => p.SeriesId == seriesId && p.AppUserId == userId && p.PagesRead > 0)
            .AnyAsync(ct);
    }

    public async Task<int> GetHighestFullyReadChapterForSeries(int seriesId, int userId, CancellationToken ct = default)
    {
        var list = await context.AppUserProgresses
            .Join(context.Chapter, appUserProgresses => appUserProgresses.ChapterId, chapter => chapter.Id,
                (appUserProgresses, chapter) => new {appUserProgresses, chapter})
            .Where(p => p.appUserProgresses.SeriesId == seriesId && p.appUserProgresses.AppUserId == userId &&
                        p.appUserProgresses.PagesRead >= p.chapter.Pages)
            .Where(p => p.chapter.MaxNumber != ParserConstants.SpecialVolumeNumber)
            .Select(p => p.chapter.MaxNumber)
            .ToListAsync(ct);
        return list.Count == 0 ? 0 : (int) list.DefaultIfEmpty().Max(d => d);
    }

    public async Task<float> GetHighestFullyReadVolumeForSeries(int seriesId, int userId, CancellationToken ct = default)
    {
        var list = await context.AppUserProgresses
            .Join(context.Chapter, appUserProgresses => appUserProgresses.ChapterId, chapter => chapter.Id,
                (appUserProgresses, chapter) => new {appUserProgresses, chapter})
            .Where(p => p.appUserProgresses.SeriesId == seriesId && p.appUserProgresses.AppUserId == userId &&
                        p.appUserProgresses.PagesRead >= p.chapter.Pages)
            .Where(p => p.chapter.MaxNumber != ParserConstants.SpecialVolumeNumber)
            .Select(p => p.chapter.Volume.MaxNumber)
            .ToListAsync(ct);

        return list.Count == 0 ? 0 : list.DefaultIfEmpty().Max();
    }

    public async Task<DateTime?> GetLatestProgressForSeries(int seriesId, int userId, CancellationToken ct = default)
    {
        var list = await context.AppUserProgresses.Where(p => p.AppUserId == userId && p.SeriesId == seriesId)
            .Select(p => p.LastModifiedUtc)
            .ToListAsync(ct);
        return list.Count == 0 ? null : list.DefaultIfEmpty().Max();
    }

    public async Task<DateTime?> GetLatestProgressForVolume(int volumeId, int userId, CancellationToken ct = default)
    {
        var list = await context.AppUserProgresses.Where(p => p.AppUserId == userId && p.VolumeId == volumeId)
            .Select(p => p.LastModifiedUtc)
            .ToListAsync(ct);
        return list.Count == 0 ? null : list.DefaultIfEmpty().Max();
    }

    public async Task<DateTime?> GetLatestProgressForChapter(int chapterId, int userId, CancellationToken ct = default)
    {
        return await context.AppUserProgresses
            .Where(p => p.AppUserId == userId && p.ChapterId == chapterId)
            .Select(p => p.LastModifiedUtc)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<DateTime?> GetFirstProgressForSeries(int seriesId, int userId, CancellationToken ct = default)
    {
        var list = await context.AppUserProgresses.Where(p => p.AppUserId == userId && p.SeriesId == seriesId)
            .Select(p => p.LastModifiedUtc)
            .ToListAsync(ct);
        return list.Count == 0 ? null : list.DefaultIfEmpty().Min();
    }

    public async Task<DateTime?> GetFirstProgressForUser(int userId, CancellationToken ct = default)
    {
        return await context.AppUserProgresses
            .Where(p => p.AppUserId == userId)
            .OrderBy(p => p.CreatedUtc)
            .Select(p => p.CreatedUtc)
            .FirstOrDefaultAsync(ct);
    }

    public async Task UpdateAllProgressThatAreMoreThanChapterPages(CancellationToken ct = default)
    {
        var updates = context.AppUserProgresses
            .Join(context.Chapter,
                progress => progress.ChapterId,
                chapter => chapter.Id,
                (progress, chapter) => new
                {
                    Progress = progress,
                    Chapter = chapter
                })
            .Where(joinResult => joinResult.Progress.PagesRead > joinResult.Chapter.Pages)
            .Select(result => new
            {
                ProgressId = result.Progress.Id,
                NewPagesRead = Math.Min(result.Progress.PagesRead, result.Chapter.Pages)
            })
            .AsEnumerable();

        // Need to run this Raw because DataContext will update LastModified on the entity which breaks ordering for progress
        var sqlBuilder = new StringBuilder();
        foreach (var update in updates)
        {
            sqlBuilder.Append($"UPDATE AppUserProgresses SET PagesRead = {update.NewPagesRead} WHERE Id = {update.ProgressId};");
        }

        // Execute the batch SQL
        var batchSql = sqlBuilder.ToString();
        await context.Database.ExecuteSqlRawAsync(batchSql, ct);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="chapterId"></param>
    /// <param name="userId">If 0, will pull all records</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<IList<FullProgressDto>> GetUserProgressForChapter(int chapterId, int userId = 0,
        CancellationToken ct = default)
    {
        return await context.AppUserProgresses
            .WhereIf(userId > 0, p => p.AppUserId == userId)
            .Where(p => p.ChapterId == chapterId)
            .Include(p => p.AppUser)
            .Select(p => new FullProgressDto()
            {
                AppUserId = p.AppUserId,
                ChapterId = p.ChapterId,
                PagesRead = p.PagesRead,
                Id = p.Id,
                Created = p.Created,
                CreatedUtc = p.CreatedUtc,
                LastModified = p.LastModified,
                LastModifiedUtc = p.LastModifiedUtc,
                UserName = p.AppUser.UserName
            })
            .ToListAsync(ct);
    }

    public Task<Dictionary<int, int>> GetUserProgressForChaptersBySeries(int userId, int seriesId, CancellationToken ct = default)
    {
        return context.AppUserProgresses
            .Where(p => p.SeriesId == seriesId && p.AppUserId == userId)
            .ToDictionaryAsync(p => p.ChapterId, p => p.PagesRead, ct);
    }

    public Task<Dictionary<int, int>> GetUserProgressForChaptersByVolumes(int userId, int seriesId, List<int> volumeIds, CancellationToken ct = default)
    {
        return context.AppUserProgresses
            .Where(p => p.SeriesId == seriesId && volumeIds.Contains(p.VolumeId) && p.AppUserId == userId)
            .ToDictionaryAsync(p => p.ChapterId, p => p.PagesRead, ct);
    }

    public Task<Dictionary<int, int>> GetUserProgressForChaptersByChapters(int userId, int seriesId, List<int> chapterIds, CancellationToken ct = default)
    {
        return context.AppUserProgresses
            .Where(p => p.SeriesId == seriesId && chapterIds.Contains(p.ChapterId) && p.AppUserId == userId)
            .ToDictionaryAsync(p => p.ChapterId, p => p.PagesRead, ct);
    }

    public async Task<AppUserProgress?> GetUserProgressAsync(int chapterId, int userId, CancellationToken ct = default)
    {
        return await context.AppUserProgresses
            .Where(p => p.ChapterId == chapterId && p.AppUserId == userId)
            .FirstOrDefaultAsync(ct);
    }
}
