using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Progress;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface IReadingSessionRepository
{
    Task<IList<ReadingSessionDto>> GetAllReadingSessionAsync(bool isActiveOnly = true);
}


public class ReadingSessionRepository(DataContext context, IMapper mapper) : IReadingSessionRepository
{
    public async Task<IList<ReadingSessionDto>> GetAllReadingSessionAsync(bool isActiveOnly = true)
    {
        // TODO: We need more restrictions based on date range
        var query = context.AppUserReadingSession
            .Where(s => !isActiveOnly || s.IsActive);

        var sessions = await query
            .Include(s => s.ActivityData)
            .Include(s => s.AppUser)
            .ToListAsync();

        if (sessions.Count == 0) return [];

        // Gather all unique IDs across ALL sessions
        var allActivityData = sessions
            .Where(s => s.ActivityData != null)
            .SelectMany(s => s.ActivityData)
            .ToList();

        var libraryIds = allActivityData.Select(a => a.LibraryId).Distinct().ToList();
        var seriesIds = allActivityData.Select(a => a.SeriesId).Distinct().ToList();
        var chapterIds = allActivityData.Select(a => a.ChapterId).Distinct().ToList();

        // Fetch all lookups in parallel - single query per table
        var libraryLookupTask = context.Library
            .Where(l => libraryIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.Name);

        var seriesLookupTask = context.Series
            .Where(s => seriesIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name);

        var chapterLookupTask = context.Chapter
            .Where(c => chapterIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.TitleName);

        await Task.WhenAll(libraryLookupTask, seriesLookupTask, chapterLookupTask);

        var libraryLookup = libraryLookupTask.Result;
        var seriesLookup = seriesLookupTask.Result;
        var chapterLookup = chapterLookupTask.Result;

        // Map all sessions with AutoMapper
        var dtos = mapper.Map<List<ReadingSessionDto>>(sessions);

        // Enrich all activity data with names
        foreach (var dto in dtos)
        {
            if (dto.ActivityData == null) continue;

            // Sort by most recent first
            dto.ActivityData = dto.ActivityData
                .OrderByDescending(a => a.EndTimeUtc)
                .ToList();


            // First activity data will be the most recent
            foreach (var activity in dto.ActivityData)
            {
                activity.LibraryName = libraryLookup.GetValueOrDefault(activity.LibraryId, string.Empty);
                activity.SeriesName = seriesLookup.GetValueOrDefault(activity.SeriesId, string.Empty);
                activity.ChapterTitle = chapterLookup.GetValueOrDefault(activity.ChapterId, string.Empty);
            }
        }

        return dtos;
    }
}
