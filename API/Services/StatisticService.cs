using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.DTOs.Statistics;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Services;

public interface IStatisticService
{
    Task<ServerStatisticsDto> GetServerStatistics();
    Task<UserReadStatistics> GetUserReadStatistics(int userId, IList<int> libraryIds);
    Task<IEnumerable<StatCount<int>>> GetYearCount();
    Task<IEnumerable<StatCount<int>>> GetTopYears();
    Task<IEnumerable<StatCount<PublicationStatus>>> GetPublicationCount();
    Task<IEnumerable<StatCount<MangaFormat>>> GetMangaFormatCount();
    Task<FileExtensionBreakdownDto> GetFileBreakdown();
    Task<IEnumerable<TopReadDto>> GetTopUsers(int days);
    Task<IEnumerable<ReadHistoryEvent>> GetReadingHistory(int userId);
    Task<IEnumerable<PagesReadOnADayCount<DateTime>>> ReadCountByDay(int userId = 0, int days = 0);
    IEnumerable<StatCount<DayOfWeek>> GetDayBreakdown();
    IEnumerable<StatCount<int>> GetPagesReadCountByYear(int userId = 0);
    IEnumerable<StatCount<int>> GetWordsReadCountByYear(int userId = 0);
    Task UpdateServerStatistics();
}

/// <summary>
/// Responsible for computing statistics for the server
/// </summary>
/// <remarks>This performs raw queries and does not use a repository</remarks>
public class StatisticService : IStatisticService
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public StatisticService(DataContext context, IMapper mapper, IUnitOfWork unitOfWork)
    {
        _context = context;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
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

        var timeSpentReading = await _context.Chapter
            .Where(c => chapterIds.Contains(c.Id))
            .SumAsync(c => c.AvgHoursToRead);

        var totalWordsRead =  (long) Math.Round(await _context.AppUserProgresses
            .Where(p => p.AppUserId == userId)
            .Where(p => libraryIds.Contains(p.LibraryId))
            .Join(_context.Chapter, p => p.ChapterId, c => c.Id, (progress, chapter) => new {chapter, progress})
            .Where(p => p.chapter.WordCount > 0)
            .SumAsync(p => p.chapter.WordCount * (p.progress.PagesRead / (1.0f * p.chapter.Pages))));

        var chaptersRead = await _context.AppUserProgresses
            .Where(p => p.AppUserId == userId)
            .Where(p => libraryIds.Contains(p.LibraryId))
            .Where(p => p.PagesRead >= _context.Chapter.Single(c => c.Id == p.ChapterId).Pages)
            .CountAsync();

        var lastActive = await _context.AppUserProgresses
            .OrderByDescending(p => p.LastModified)
            .Where(p => p.AppUserId == userId)
            .Select(p => p.LastModified)
            .FirstOrDefaultAsync();


        // First get the total pages per library
        var totalPageCountByLibrary = _context.Chapter
            .Join(_context.Volume, c => c.VolumeId, v => v.Id, (chapter, volume) => new { chapter, volume })
            .Join(_context.Series, g => g.volume.SeriesId, s => s.Id, (g, series) => new { g.chapter, series })
            .AsEnumerable()
            .GroupBy(g => g.series.LibraryId)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.chapter.Pages));

        var totalProgressByLibrary = await _context.AppUserProgresses
            .Where(p => p.AppUserId == userId)
            .Where(p => p.LibraryId > 0)
            .GroupBy(p => p.LibraryId)
            .Select(g => new StatCount<float>
            {
                Count = g.Key,
                Value = g.Sum(p => p.PagesRead) / (float) totalPageCountByLibrary[g.Key]
            })
            .ToListAsync();


        var averageReadingTimePerWeek = _context.AppUserProgresses
            .Where(p => p.AppUserId == userId)
            .Join(_context.Chapter, p => p.ChapterId, c => c.Id,
                (p, c) => (p.PagesRead / (float) c.Pages) * c.AvgHoursToRead)
            .Average() / 7.0;

        return new UserReadStatistics()
        {
            TotalPagesRead = totalPagesRead,
            TotalWordsRead = totalWordsRead,
            TimeSpentReading = timeSpentReading,
            ChaptersRead = chaptersRead,
            LastActive = lastActive,
            PercentReadPerLibrary = totalProgressByLibrary,
            AvgHoursPerWeekSpentReading = averageReadingTimePerWeek
        };
    }

    /// <summary>
    /// Returns the Release Years and their count
    /// </summary>
    /// <returns></returns>
    public async Task<IEnumerable<StatCount<int>>> GetYearCount()
    {
        return await _context.SeriesMetadata
            .Where(sm => sm.ReleaseYear != 0)
            .AsSplitQuery()
            .GroupBy(sm => sm.ReleaseYear)
            .Select(sm => new StatCount<int>
            {
                Value = sm.Key,
                Count = _context.SeriesMetadata.Where(sm2 => sm2.ReleaseYear == sm.Key).Distinct().Count()
            })
            .OrderByDescending(d => d.Value)
            .ToListAsync();
    }

    public async Task<IEnumerable<StatCount<int>>> GetTopYears()
    {
        return await _context.SeriesMetadata
            .Where(sm => sm.ReleaseYear != 0)
            .AsSplitQuery()
            .GroupBy(sm => sm.ReleaseYear)
            .Select(sm => new StatCount<int>
            {
                Value = sm.Key,
                Count = _context.SeriesMetadata.Where(sm2 => sm2.ReleaseYear == sm.Key).Distinct().Count()
            })
            .OrderByDescending(d => d.Count)
            .Take(5)
            .ToListAsync();
    }



    public async Task<IEnumerable<StatCount<PublicationStatus>>> GetPublicationCount()
    {
        return await _context.SeriesMetadata
            .AsSplitQuery()
            .GroupBy(sm => sm.PublicationStatus)
            .Select(sm => new StatCount<PublicationStatus>
            {
                Value = sm.Key,
                Count = _context.SeriesMetadata.Where(sm2 => sm2.PublicationStatus == sm.Key).Distinct().Count()
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<StatCount<MangaFormat>>> GetMangaFormatCount()
    {
        return await _context.MangaFile
            .AsSplitQuery()
            .GroupBy(sm => sm.Format)
            .Select(mf => new StatCount<MangaFormat>
            {
                Value = mf.Key,
                Count = _context.MangaFile.Where(mf2 => mf2.Format == mf.Key).Distinct().Count()
            })
            .ToListAsync();
    }


    public async Task<ServerStatisticsDto> GetServerStatistics()
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
            .Where(sm => sm.LibraryId > 0)
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

        // Remember: Ordering does not apply if there is a distinct
        var recentlyRead = _context.AppUserProgresses
            .Join(_context.Series, p => p.SeriesId, s => s.Id,
                (appUserProgresses, series) => new
                {
                    Series = series,
                    AppUserProgresses = appUserProgresses
                })
            .AsEnumerable()
            .DistinctBy(s => s.AppUserProgresses.SeriesId)
            .OrderByDescending(x => x.AppUserProgresses.LastModified)
            .Select(x => _mapper.Map<SeriesDto>(x.Series))
            .Take(5);


        var distinctPeople = _context.Person
            .AsSplitQuery()
            .AsEnumerable()
            .GroupBy(sm => sm.NormalizedName)
            .Select(sm => sm.Key)
            .Distinct()
            .Count();

        return new ServerStatisticsDto()
        {
            ChapterCount = await _context.Chapter.CountAsync(),
            SeriesCount = await _context.Series.CountAsync(),
            TotalFiles = await _context.MangaFile.CountAsync(),
            TotalGenres = await _context.Genre.CountAsync(),
            TotalPeople = distinctPeople,
            TotalSize = await _context.MangaFile.SumAsync(m => m.Bytes),
            TotalTags = await _context.Tag.CountAsync(),
            VolumeCount = await _context.Volume.Where(v => v.Number != 0).CountAsync(),
            MostActiveUsers = mostActiveUsers,
            MostActiveLibraries = mostActiveLibrary,
            MostPopularSeries = mostPopularSeries,
            MostReadSeries = mostReadSeries,
            RecentlyRead = recentlyRead
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
                .OrderBy(d => d.TotalFiles)
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
                UserName = _context.AppUser.Single(u2 => u2.Id == userId).UserName,
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

    public async Task<IEnumerable<PagesReadOnADayCount<DateTime>>> ReadCountByDay(int userId = 0, int days = 0)
    {
        var query = _context.AppUserProgresses
            .AsSplitQuery()
            .AsNoTracking()
            .Join(_context.Chapter, appUserProgresses => appUserProgresses.ChapterId, chapter => chapter.Id,
                (appUserProgresses, chapter) => new {appUserProgresses, chapter})
            .Join(_context.Volume, x => x.chapter.VolumeId, volume => volume.Id,
                (x, volume) => new {x.appUserProgresses, x.chapter, volume})
            .Join(_context.Series, x => x.appUserProgresses.SeriesId, series => series.Id,
                (x, series) => new {x.appUserProgresses, x.chapter, x.volume, series});

        if (userId > 0)
        {
            query = query.Where(x => x.appUserProgresses.AppUserId == userId);
        }

        if (days > 0)
        {
            var date = DateTime.Now.AddDays(days * -1);
            query = query.Where(x => x.appUserProgresses.LastModified >= date && x.appUserProgresses.Created >= date);
        }

        var results = await query.GroupBy(x => new
            {
                Day = x.appUserProgresses.Created.Date,
                x.series.Format
            })
            .Select(g => new PagesReadOnADayCount<DateTime>
            {
                Value = g.Key.Day,
                Format = g.Key.Format,
                Count = g.Count()
            })
            .OrderBy(d => d.Value)
            .ToListAsync();

        if (results.Count > 0)
        {
            var minDay = results.Min(d => d.Value);
            for (var date = minDay; date < DateTime.Now; date = date.AddDays(1))
            {
                if (results.Any(d => d.Value == date)) continue;
                results.Add(new PagesReadOnADayCount<DateTime>()
                {
                    Format = MangaFormat.Unknown,
                    Value = date,
                    Count = 0
                });
            }
        }

        return results;
    }

    public IEnumerable<StatCount<DayOfWeek>> GetDayBreakdown()
    {
        return _context.AppUserProgresses
            .AsSplitQuery()
            .AsNoTracking()
            .GroupBy(p => p.LastModified.DayOfWeek)
            .OrderBy(g => g.Key)
            .Select(g => new StatCount<DayOfWeek>{ Value = g.Key, Count = g.Count() })
            .AsEnumerable();
    }

    /// <summary>
    /// Return a list of years for the given userId
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public IEnumerable<StatCount<int>> GetPagesReadCountByYear(int userId = 0)
    {
        var query = _context.AppUserProgresses
            .AsSplitQuery()
            .AsNoTracking();

        if (userId > 0)
        {
            query = query.Where(p => p.AppUserId == userId);
        }

        return query.GroupBy(p => p.LastModified.Year)
            .OrderBy(g => g.Key)
            .Select(g => new StatCount<int> {Value = g.Key, Count = g.Sum(x => x.PagesRead)})
            .AsEnumerable();
    }

    public IEnumerable<StatCount<int>> GetWordsReadCountByYear(int userId = 0)
    {
        var query = _context.AppUserProgresses
            .AsSplitQuery()
            .AsNoTracking();

        if (userId > 0)
        {
            query = query.Where(p => p.AppUserId == userId);
        }

        return query
            .Join(_context.Chapter, p => p.ChapterId, c => c.Id, (progress, chapter) => new {chapter, progress})
            .Where(p => p.chapter.WordCount > 0)
            .GroupBy(p => p.progress.LastModified.Year)
            .Select(g => new StatCount<int>{
                Value = g.Key,
                Count = (long) Math.Round(g.Sum(p => p.chapter.WordCount * ((1.0f * p.progress.PagesRead) / p.chapter.Pages)))
            })
            .AsEnumerable();
    }

    /// <summary>
    /// Updates the ServerStatistics table for the current year
    /// </summary>
    /// <remarks>This commits</remarks>
    /// <returns></returns>
    public async Task UpdateServerStatistics()
    {
        var year = DateTime.Today.Year;

        var existingRecord = await _context.ServerStatistics.SingleOrDefaultAsync(s => s.Year == year) ?? new ServerStatistics();

        existingRecord.Year = year;
        existingRecord.ChapterCount = await _context.Chapter.CountAsync();
        existingRecord.VolumeCount = await _context.Volume.CountAsync();
        existingRecord.FileCount = await _context.MangaFile.CountAsync();
        existingRecord.SeriesCount = await _context.Series.CountAsync();
        existingRecord.UserCount = await _context.Users.CountAsync();
        existingRecord.GenreCount = await _context.Genre.CountAsync();
        existingRecord.TagCount = await _context.Tag.CountAsync();
        existingRecord.PersonCount =  _context.Person
            .AsSplitQuery()
            .AsEnumerable()
            .GroupBy(sm => sm.NormalizedName)
            .Select(sm => sm.Key)
            .Distinct()
            .Count();

        _context.ServerStatistics.Attach(existingRecord);
        if (existingRecord.Id > 0)
        {
            _context.Entry(existingRecord).State = EntityState.Modified;
        }
        await _unitOfWork.CommitAsync();
    }

    public async Task<IEnumerable<TopReadDto>> GetTopUsers(int days)
    {
        var libraries = (await _unitOfWork.LibraryRepository.GetLibrariesAsync()).ToList();
        var users = (await _unitOfWork.UserRepository.GetAllUsersAsync()).ToList();
        var minDate = DateTime.Now.Subtract(TimeSpan.FromDays(days));

        var topUsersAndReadChapters = _context.AppUserProgresses
            .AsSplitQuery()
            .AsEnumerable()
            .GroupBy(sm => sm.AppUserId)
            .Select(sm => new
            {
                User = _context.AppUser.Single(u => u.Id == sm.Key),
                Chapters = _context.Chapter.Where(c => _context.AppUserProgresses
                    .Where(u => u.AppUserId == sm.Key)
                    .Where(p => p.PagesRead > 0)
                    .Where(p => days == 0 || (p.Created >= minDate && p.LastModified >= minDate))
                    .Select(p => p.ChapterId)
                    .Distinct()
                    .Contains(c.Id))
            })
            .OrderByDescending(d => d.Chapters.Sum(c => c.AvgHoursToRead))
            .Take(5)
            .ToList();


        // Need a mapping of Library to chapter ids
        var chapterIdWithLibraryId = topUsersAndReadChapters
            .SelectMany(u => u.Chapters
                .Select(c => c.Id)).Select(d => new
                    {
                        LibraryId = _context.Chapter.Where(c => c.Id == d).AsSplitQuery().Select(c => c.Volume).Select(v => v.Series).Select(s => s.LibraryId).Single(),
                        ChapterId = d
                    })
            .ToList();

        var chapterLibLookup = new Dictionary<int, int>();
        foreach (var cl in chapterIdWithLibraryId.Where(cl => !chapterLibLookup.ContainsKey(cl.ChapterId)))
        {
            chapterLibLookup.Add(cl.ChapterId, cl.LibraryId);
        }

        var user = new Dictionary<int, Dictionary<LibraryType, long>>();
        foreach (var userChapter in topUsersAndReadChapters)
        {
            if (!user.ContainsKey(userChapter.User.Id)) user.Add(userChapter.User.Id, new Dictionary<LibraryType, long>());
            var libraryTimes = user[userChapter.User.Id];

            foreach (var chapter in userChapter.Chapters)
            {
                var library = libraries.First(l => l.Id == chapterLibLookup[chapter.Id]);
                if (!libraryTimes.ContainsKey(library.Type)) libraryTimes.Add(library.Type, 0L);
                var existingHours = libraryTimes[library.Type];
                libraryTimes[library.Type] = existingHours + chapter.AvgHoursToRead;
            }

            user[userChapter.User.Id] = libraryTimes;
        }

        return user.Keys.Select(userId => new TopReadDto()
            {
                UserId = userId,
                Username = users.First(u => u.Id == userId).UserName,
                BooksTime = user[userId].ContainsKey(LibraryType.Book) ? user[userId][LibraryType.Book] : 0,
                ComicsTime = user[userId].ContainsKey(LibraryType.Comic) ? user[userId][LibraryType.Comic] : 0,
                MangaTime = user[userId].ContainsKey(LibraryType.Manga) ? user[userId][LibraryType.Manga] : 0,
            })
            .ToList();
    }
}
