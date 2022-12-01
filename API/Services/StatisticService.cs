using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.DTOs.Statistics;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Services;

public interface IStatisticService
{
    Task<ServerStatistics> GetServerStatistics();
    Task<UserReadStatistics> GetUserReadStatistics(int userId, IList<int> libraryIds);
    Task<IEnumerable<YearCountDto>> GetYearCount();
    Task<IEnumerable<YearCountDto>> GetTopYears();
    Task<IEnumerable<PublicationCountDto>> GetPublicationCount();
    Task<IEnumerable<MangaFormatCountDto>> GetMangaFormatCount();
    Task<FileExtensionBreakdownDto> GetFileBreakdown();
    Task<IEnumerable<TopReadDto>> GetTopUsers(int days);
    Task<IEnumerable<ReadHistoryEvent>> GetReadingHistory(int userId);
}

/// <summary>
/// Responsible for computing statistics for the server
/// </summary>
/// <remarks>This performs raw queries and does not use a repository</remarks>
public class StatisticService : IStatisticService
{
    private readonly DataContext _context;
    private readonly ILogger<StatisticService> _logger;
    private readonly IMapper _mapper;

    public StatisticService(DataContext context, ILogger<StatisticService> logger, IMapper mapper)
    {
        _context = context;
        _logger = logger;
        _mapper = mapper;
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

        var chaptersRead = await _context.AppUserProgresses
            .Where(p => p.AppUserId == userId)
            .Where(p => libraryIds.Contains(p.LibraryId))
            .Where(p => p.PagesRead >= _context.Chapter.Single(c => c.Id == p.ChapterId).Pages)
            .CountAsync();

        var lastActive = await _context.AppUserProgresses
            .OrderByDescending(p => p.LastModified)
            .Select(p => p.LastModified)
            .FirstOrDefaultAsync();

        //var

        return new UserReadStatistics()
        {
            TotalPagesRead = totalPagesRead,
            TimeSpentReading = timeSpentReading,
            ChaptersRead = chaptersRead,
            LastActive = lastActive,
            //AvgHoursPerWeekSpentReading =
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


    public async Task<ServerStatistics> GetServerStatistics()
    {
        var mostActiveUsers = _context.AppUserProgresses
            .AsSplitQuery()
            .AsEnumerable()
            .GroupBy(sm => sm.AppUserId)
            .Select(sm => new StatCount<UserDto>
            {
                Value = _context.AppUser.Where(u => u.Id == sm.Key).ProjectTo<UserDto>(_mapper.ConfigurationProvider)
                    .Single(),
                Count = _context.AppUserProgresses.Where(u => u.AppUserId == sm.Key).Distinct().Count()
            })
            .OrderByDescending(d => d.Count)
            .Take(5);

        var mostActiveLibrary = _context.AppUserProgresses
            .AsSplitQuery()
            .AsEnumerable()
            .GroupBy(sm => sm.LibraryId)
            .Select(sm => new StatCount<LibraryDto>
            {
                Value = _context.Library.Where(u => u.Id == sm.Key).ProjectTo<LibraryDto>(_mapper.ConfigurationProvider)
                    .Single(),
                Count = _context.AppUserProgresses.Where(u => u.LibraryId == sm.Key).Distinct().Count()
            })
            .OrderByDescending(d => d.Count)
            .Take(5);

        var mostPopularSeries = _context.AppUserProgresses
            .AsSplitQuery()
            .AsEnumerable()
            .GroupBy(sm => sm.SeriesId)
            .Select(sm => new StatCount<SeriesDto>
            {
                Value = _context.Series.Where(u => u.Id == sm.Key).ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
                    .Single(),
                Count = _context.AppUserProgresses.Where(u => u.SeriesId == sm.Key).Distinct().Count()
            })
            .OrderByDescending(d => d.Count)
            .Take(5);

        var mostReadSeries = _context.AppUserProgresses
            .AsSplitQuery()
            .AsEnumerable()
            .GroupBy(sm => sm.SeriesId)
            .Select(sm => new StatCount<SeriesDto>
            {
                Value = _context.Series.Where(u => u.Id == sm.Key).ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
                    .Single(),
                Count = _context.AppUserProgresses.Where(u => u.SeriesId == sm.Key).AsEnumerable().DistinctBy(p => p.AppUserId).Count()
            })
            .OrderByDescending(d => d.Count)
            .Take(5);


        return new ServerStatistics()
        {
            ChapterCount = await _context.Chapter.CountAsync(),
            SeriesCount = await _context.Series.CountAsync(),
            TotalFiles = await _context.MangaFile.CountAsync(),
            TotalGenres = await _context.Genre.CountAsync(),
            TotalPeople = await _context.Person.CountAsync(),
            TotalSize = await _context.MangaFile.SumAsync(m => m.Bytes),
            TotalTags = await _context.Tag.CountAsync(),
            VolumeCount = await _context.Volume.Where(v => v.Number != 0).CountAsync(),
            MostActiveUsers = mostActiveUsers,
            MostActiveLibraries = mostActiveLibrary,
            MostPopularSeries = mostPopularSeries,
            MostReadSeries = mostReadSeries
        };
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

    public async Task<IEnumerable<ReadHistoryEvent>> GetReadingHistory(int userId)
    {
        return await _context.AppUserProgresses
            .Where(u => u.AppUserId == userId)
            .AsNoTracking()
            .AsSplitQuery()
            .Select(u => new ReadHistoryEvent
            {
                UserId = u.AppUserId,
                UserName = _context.AppUser.Single(u => u.Id == userId).UserName,
                SeriesName = _context.Series.Single(s => s.Id == u.SeriesId).Name,
                SeriesId = u.SeriesId,
                LibraryId = u.LibraryId,
                ReadDate = u.LastModified,
                ChapterId = u.ChapterId,
                ChapterNumber = _context.Chapter.Single(c => c.Id == u.ChapterId).Number
            })
            .OrderByDescending(d => d.ReadDate)
            .ToListAsync();
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
    //     // Goal: Get a list of users ordered by read time for a given library type
    //     query.Join(_context.Series, p => p.SeriesId, s => s.Id, (progress, series) => new
    //     {
    //
    //     });
    //
    //     _context.Series
    //         .Where(s => s.Library.Type == type)
    //         .AsSplitQuery()
    //         .GroupBy(s => s.Format)
    //         .Select(sm => new
    //         {
    //             Format = sm.Key,
    //             Count = _context.SeriesMetadata.Where(sm2 => sm2.ReleaseYear == sm.Key).Distinct().Count()
    //         })
    //         .OrderByDescending(d => d.Count)
    //         .Take(5)
    //         .ToListAsync();
    //
    //     var users = query.Select(p => new
    //     {
    //         p.PagesRead,
    //         p.AppUserId,
    //         p.SeriesId,
    //         p.LibraryId,
    //     });
    //
    //
    //
    //     //var allMangaSeriesIds = query.Select(p => p.SeriesId).AsEnumerable();
    //
    //     return _context.Series
    //         //.Where(s => allMangaSeriesIds.Contains(s.Id) && s.Library.Type == type)
    //         .Where(s => s.Library.Type == type)
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
