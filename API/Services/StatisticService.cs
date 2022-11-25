using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Statistics;
using API.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Services;

public interface IStatisticService
{
    Task<UserReadStatistics> GetUserReadStatistics(int userId, IList<int> libraryIds);
    Task<IEnumerable<YearSpread>> GetYearSpread();
}

/// <summary>
/// Responsible for computing statistics for the server
/// </summary>
/// <remarks>This performs raw queries and does not use a repository</remarks>
public class StatisticService : IStatisticService
{
    private readonly DataContext _context;
    private readonly ILogger<StatisticService> _logger;

    public StatisticService(DataContext context, ILogger<StatisticService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<UserReadStatistics> GetUserReadStatistics(int userId, IList<int> libraryIds)
    {
        if (libraryIds.Count == 0)
            libraryIds = await _context.Library.GetUserLibraries(userId).ToListAsync();


        // Total Pages Read
        var totalPagesRead = await _context.AppUserProgresses
            .Where(p => p.AppUserId == userId)
            .SumAsync(p => p.PagesRead);

        var ids = await _context.AppUserProgresses
            .Where(p => p.AppUserId == userId)
            .Where(p => p.PagesRead > 0)
            .Select(p => new {p.ChapterId, p.SeriesId})
            .ToListAsync();

        var chapterIds = ids.Select(id => id.ChapterId);
        var seriesIds = ids.Select(id => id.SeriesId);

        var timeSpentReading = await _context.Chapter
            .Where(c => chapterIds.Contains(c.Id))
            .SumAsync(c => c.AvgHoursToRead);

        // Maybe make this top 5 genres? But usually there are 3-5 genres that are always common...
        // Maybe use rating to calculate top genres?
        // var genres = await _context.Series
        //     .Where(s => seriesIds.Contains(s.Id))
        //     .Select(s => s.Metadata)
        //     .SelectMany(sm => sm.Genres)
        //     //.DistinctBy(g => g.NormalizedTitle)
        //     .ToListAsync();

        // How many series of each format have you read? (Epub, Archive, etc)

        // Percentage of libraries read. For each library, get the total pages vs read
        var allLibraryIds = await _context.Library.GetUserLibraries(userId).ToListAsync();


        return new UserReadStatistics()
        {
            TotalPagesRead = totalPagesRead,
            TimeSpentReading = timeSpentReading
        };
    }

    /// <summary>
    /// Returns the Release Years and their count
    /// </summary>
    /// <returns></returns>
    public async Task<IEnumerable<YearSpread>> GetYearSpread()
    {
        return await _context.SeriesMetadata
            .Where(sm => sm.ReleaseYear != 0)
            .AsSplitQuery()
            .Select(sm => new YearSpread
            {
                ReleaseYear = sm.ReleaseYear,
                Count = _context.SeriesMetadata.Where(sm2 => sm2.Id == sm.Id).Distinct().Count()
            })
            .ToListAsync();
    }
}
