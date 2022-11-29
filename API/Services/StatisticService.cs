﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Statistics;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Services;

public interface IStatisticService
{
    Task<UserReadStatistics> GetUserReadStatistics(int userId, IList<int> libraryIds);
    Task<IEnumerable<YearCountDto>> GetYearCount();
    Task<IEnumerable<YearCountDto>> GetTopYears();
    Task<IEnumerable<PublicationCountDto>> GetPublicationCount();
    Task<IEnumerable<MangaFormatCountDto>> GetMangaFormatCount();
    Task<ServerStatistics> GetServerStatistics();
    Task<FileExtensionBreakdownDto> GetFileBreakdown();
    Task<IEnumerable<TopReadDto>> GetTopUsers(int days);
}

/// <summary>
/// Responsible for computing statistics for the server
/// </summary>
/// <remarks>This performs raw queries and does not use a repository</remarks>
public class StatisticService : IStatisticService
{
    private readonly DataContext _context;
    private readonly ILogger<StatisticService> _logger;
    private readonly IDirectoryService _directoryService;

    public StatisticService(DataContext context, ILogger<StatisticService> logger, IDirectoryService directoryService)
    {
        _context = context;
        _logger = logger;
        _directoryService = directoryService;
    }

    public async Task<UserReadStatistics> GetUserReadStatistics(int userId, IList<int> libraryIds)
    {
        if (libraryIds.Count == 0)
            libraryIds = await _context.Library.GetUserLibraries(userId).ToListAsync();


        // Total Pages Read
        var totalPagesRead = await _context.AppUserProgresses
            .Where(p => p.AppUserId == userId)
            .Where(p => libraryIds.Contains(p.LibraryId))
            .SumAsync(p => p.PagesRead);

        var ids = await _context.AppUserProgresses
            .Where(p => p.AppUserId == userId)
            .Where(p => libraryIds.Contains(p.LibraryId))
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
        //var allLibraryIds = await _context.Library.GetUserLibraries(userId).ToListAsync();


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
    public async Task<IEnumerable<YearCountDto>> GetYearCount()
    {
        return await _context.SeriesMetadata
            .Where(sm => sm.ReleaseYear != 0)
            .AsSplitQuery()
            .GroupBy(sm => sm.ReleaseYear)
            .Select(sm => new YearCountDto
            {
                Value = sm.Key,
                Count = _context.SeriesMetadata.Where(sm2 => sm2.ReleaseYear == sm.Key).Distinct().Count()
            })
            .OrderByDescending(d => d.Value)
            .ToListAsync();
    }

    public async Task<IEnumerable<YearCountDto>> GetTopYears()
    {
        return await _context.SeriesMetadata
            .Where(sm => sm.ReleaseYear != 0)
            .AsSplitQuery()
            .GroupBy(sm => sm.ReleaseYear)
            .Select(sm => new YearCountDto
            {
                Value = sm.Key,
                Count = _context.SeriesMetadata.Where(sm2 => sm2.ReleaseYear == sm.Key).Distinct().Count()
            })
            .OrderByDescending(d => d.Count)
            .Take(5)
            .ToListAsync();
    }



    public async Task<IEnumerable<PublicationCountDto>> GetPublicationCount()
    {
        return await _context.SeriesMetadata
            .AsSplitQuery()
            .GroupBy(sm => sm.PublicationStatus)
            .Select(sm => new PublicationCountDto
            {
                Value = sm.Key,
                Count = _context.SeriesMetadata.Where(sm2 => sm2.PublicationStatus == sm.Key).Distinct().Count()
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<MangaFormatCountDto>> GetMangaFormatCount()
    {
        return await _context.MangaFile
            .AsSplitQuery()
            .GroupBy(sm => sm.Format)
            .Select(mf => new MangaFormatCountDto
            {
                Value = mf.Key,
                Count = _context.MangaFile.Where(mf2 => mf2.Format == mf.Key).Distinct().Count()
            })
            .ToListAsync();
    }


    public Task<ServerStatistics> GetServerStatistics()
    {
        return Task.FromResult(new ServerStatistics());
    }

    public async Task<FileExtensionBreakdownDto> GetFileBreakdown()
    {
        return new FileExtensionBreakdownDto()
        {
            FileBreakdown = await _context.MangaFile
                .AsSplitQuery()
                .AsNoTracking()
                .GroupBy(sm => sm.Extension)
                .Select(mf => new FileExtensionDto()
                {
                    Extension = mf.Key,
                    Format =_context.MangaFile.Where(mf2 => mf2.Extension == mf.Key).Select(mf2 => mf2.Format).Single(),
                    TotalSize = _context.MangaFile.Where(mf2 => mf2.Extension == mf.Key).Distinct().Sum(mf2 => mf2.Bytes),
                    TotalFiles = _context.MangaFile.Where(mf2 => mf2.Extension == mf.Key).Distinct().Count()
                })
                .ToListAsync(),
            TotalFileSize = await _context.MangaFile
                .AsNoTracking()
                .AsSplitQuery()
                .SumAsync(f => f.Bytes)
        };
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="userId">If 0, all users will be returned back</param>
    /// <param name="days">If 0, a date clamp will not take place</param>
    /// <returns></returns>
    // public Task<TopReadsDto> GetTopReads(int userId = 0, int days = 0)
    // {
    //     return Task.FromResult(new TopReadsDto()
    //     {
    //         Manga = GetTopReadDtosForFormat(userId, days, LibraryType.Manga).AsEnumerable(),
    //         Comics = GetTopReadDtosForFormat(userId, days, LibraryType.Comic).AsEnumerable(),
    //         Books = GetTopReadDtosForFormat(userId, days, LibraryType.Book).AsEnumerable(),
    //     });
    // }

    public Task<IEnumerable<TopReadDto>> GetTopUsers(int days)
    {
        throw new NotImplementedException();
    }

    // private IEnumerable<TopReadDto> GetTopReadDtosForFormat(int days, LibraryType type)
    // {
    //     var minDate = DateTime.Now.Subtract(TimeSpan.FromDays(days));
    //     var query = _context.AppUserProgresses
    //         .AsSplitQuery()
    //         .AsNoTracking();
    //
    //     if (days > 0) query = query.Where(p => p.LastModified > minDate);
    //
    //     var users = query.Select(p => new
    //     {
    //         p.PagesRead,
    //         p.AppUserId,
    //         p.SeriesId
    //     })
    //
    //     var allMangaSeriesIds = query.Select(p => p.SeriesId).AsEnumerable();
    //
    //     return _context.Series
    //         .Where(s => allMangaSeriesIds.Contains(s.Id) && s.Library.Type == type)
    //         .Select(s => new TopReadDto()
    //         {
    //
    //             SeriesId = s.Id,
    //             SeriesName = s.Name,
    //             LibraryId = s.LibraryId,
    //             UsersRead = _context.AppUserProgresses
    //                 .Where(p => p.SeriesId == s.Id)
    //                 .Select(p => p.AppUserId)
    //                 .Distinct()
    //                 .Count()
    //         })
    //         .OrderBy(d => d.UsersRead)
    //         .Take(5);
    //
    // }
}
