using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.ManualMigrations;
using API.DTOs;
using API.DTOs.Metadata;
using API.DTOs.Person;
using API.DTOs.ReadingLists;
using API.DTOs.Statistics;
using API.DTOs.Stats;
using API.DTOs.Stats.V3.ClientDevice;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Enums.UserPreferences;
using API.Extensions;
using API.Extensions.QueryExtensions;
using API.Extensions.QueryExtensions.Filtering;
using API.Services.Tasks.Scanner.Parser;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Services;
#nullable enable

internal sealed record UserReadCount(int ReadingListId, int AppUserId, int ChaptersRead);

public interface IStatisticService
{
    Task<ServerStatisticsDto> GetServerStatistics();
    Task<UserReadStatistics> GetUserReadStatistics(int userId, IList<int> libraryIds);
    Task<IEnumerable<StatCount<int>>> GetYearCount();
    Task<IEnumerable<StatCount<int>>> GetTopYears();
    Task<IList<StatBucketDto>> GetPopularDecades();
    Task<IList<StatCount<LibraryDto>>> GetPopularLibraries();
    Task<IList<StatCount<SeriesDto>>> GetPopularSeries();
    Task<IList<StatCount<ReadingListDto>>> GetPopularReadingList(int take = 5);
    Task<IList<StatCount<GenreTagDto>>> GetPopularGenres();
    Task<IList<StatCount<TagDto>>> GetPopularTags();
    Task<IList<StatCount<PersonDto>>> GetPopularPerson(PersonRole role);
    Task<IEnumerable<StatCount<PublicationStatus>>> GetPublicationCount();
    Task<IEnumerable<StatCount<MangaFormat>>> GetMangaFormatCount();
    Task<FileExtensionBreakdownDto> GetFileBreakdown();
    Task<IEnumerable<TopReadDto>> GetTopUsers(int days);
    Task<IEnumerable<ReadHistoryEvent>> GetReadingHistory(int userId);
    Task<IEnumerable<StatCountWithFormat<DateTime>>> ReadCountByDay(int userId = 0, int days = 0);
    Task<IEnumerable<StatCountWithFormat<DateTime>>> ReadCounts(StatsFilterDto filter, int userId = 0);
    Task<IList<StatCount<DayOfWeek>>> GetDayBreakdown(int userId = 0);
    Task<IList<StatCount<int>>> GetPagesReadCountByYear(int userId = 0);
    Task<IList<StatCount<int>>> GetWordsReadCountByYear(int userId = 0);
    Task UpdateServerStatistics();
    Task<IEnumerable<FileExtensionExportDto>> GetFilesByExtension(string fileExtension);
    Task<DeviceClientBreakdownDto> GetClientTypeBreakdown(DateTime fromDateUtc);
    Task<IList<StatCount<string>>> GetDeviceTypeCounts(DateTime fromDateUtc);
    Task<ReadingActivityGraphDto> GetReadingActivityGraphData(StatsFilterDto filter, int userId, int year, int requestingUserId);
    Task<ReadingPaceDto> GetReadingPaceForUser(StatsFilterDto filter, int userId, int year, bool booksOnly, int requestingUserId);
    Task<BreakDownDto<string>> GetGenreBreakdownForUser(StatsFilterDto filter, int userId, int requestingUserId);
    Task<BreakDownDto<string>> GetTagBreakdownForUser(StatsFilterDto filter, int userId, int requestingUserId);
    Task<SpreadStatsDto> GetPageSpreadForUser(StatsFilterDto filter, int userId, int requestingUserId);
    Task<SpreadStatsDto> GetWordSpreadForUser(StatsFilterDto filter, int userId, int requestingUserId);
    Task<IList<StatCount<YearMonthGroupingDto>>> GetReadsPerMonth(StatsFilterDto filter, int userId, int requestingUserId);
    Task<IList<MostReadAuthorsDto>> GetMostReadAuthors(StatsFilterDto filter, int userId, int requestingUserId);
    Task<int> GetTotalReads(int userId, int requestingUserId);
    Task<ReadTimeByHourDto?> GetTimeReadingByHour(StatsFilterDto filter, int userId, int requestingUserId);
    Task<ProfileStatBarDto> GetUserStatBar(StatsFilterDto filter, int userId, int requestingUserId);
    Task<IList<MostActiveUserDto>> GetMostActiveUsers(StatsFilterDto filter);
    Task<IList<StatCountWithFormat<DateTime>>> GetFilesAddedOverTime();
}

/// <summary>
/// Responsible for computing statistics for the server
/// </summary>
/// <remarks>This performs raw queries and does not use a repository</remarks>
public class StatisticService(ILogger<StatisticService> logger, DataContext context, IMapper mapper, IUnitOfWork unitOfWork): IStatisticService
{

    public async Task<UserReadStatistics> GetUserReadStatistics(int userId, IList<int> libraryIds)
    {
        if (libraryIds.Count == 0)
        {
            libraryIds = await context.Library.GetUserLibraries(userId).ToListAsync();
        }

        var activityData = await context.AppUserReadingSessionActivityData
            .AsNoTracking()
            .Where(a => a.ReadingSession.AppUserId == userId)
            .Where(a => libraryIds.Contains(a.LibraryId))
            .Select(a => new
            {
                a.PagesRead,
                a.WordsRead,
                a.TotalPages,
                a.StartTimeUtc,
                a.EndTimeUtc,
                a.LibraryId,
                a.ChapterId
            })
            .ToListAsync();

        var totalPagesRead = activityData.Sum(a => a.PagesRead);

        var totalWordsRead = activityData.Sum(a => (long)a.WordsRead);

        var timeSpentReading = (long)Math.Round(activityData
            .Where(a => a.EndTimeUtc != null)
            .Sum(a => (a.EndTimeUtc!.Value - a.StartTimeUtc).TotalHours));

        var lastActive = await context.AppUserReadingSession
            .AsNoTracking()
            .Where(s => s.AppUserId == userId)
            .Select(s => s.EndTimeUtc)
            .DefaultIfEmpty()
            .MaxAsync();

        // Average reading time per week
        var earliestReadDate = activityData
            .Select(a => a.StartTimeUtc)
            .DefaultIfEmpty(DateTime.UtcNow)
            .Min();

        var avgHoursPerWeek = 0f;
        if (activityData.Count > 0 && earliestReadDate != DateTime.UtcNow)
        {
            var timeDifference = DateTime.UtcNow - earliestReadDate;
            var deltaWeeks = Math.Max(1, (int)Math.Ceiling(timeDifference.TotalDays / 7));
            avgHoursPerWeek = (float)timeSpentReading / deltaWeeks;
        }

        return new UserReadStatistics
        {
            TotalPagesRead = totalPagesRead,
            TotalWordsRead = totalWordsRead,
            TimeSpentReading = timeSpentReading,
            LastActiveUtc = lastActive,
            AvgHoursPerWeekSpentReading = avgHoursPerWeek
        };
    }
    /// <summary>
    /// Returns the Release Years and their count
    /// </summary>
    /// <returns></returns>
    public async Task<IEnumerable<StatCount<int>>> GetYearCount()
    {
        return await context.SeriesMetadata
            .Where(sm => sm.ReleaseYear != 0)
            .AsSplitQuery()
            .GroupBy(sm => sm.ReleaseYear)
            .Select(sm => new StatCount<int>
            {
                Value = sm.Key,
                Count = context.SeriesMetadata.Where(sm2 => sm2.ReleaseYear == sm.Key).Distinct().Count()
            })
            .OrderByDescending(d => d.Value)
            .ToListAsync();
    }

    public async Task<IEnumerable<StatCount<int>>> GetTopYears()
    {
        return await context.SeriesMetadata
            .Where(sm => sm.ReleaseYear != 0)
            .AsSplitQuery()
            .GroupBy(sm => sm.ReleaseYear)
            .Select(sm => new StatCount<int>
            {
                Value = sm.Key,
                Count = context.SeriesMetadata.Where(sm2 => sm2.ReleaseYear == sm.Key).Distinct().Count()
            })
            .OrderByDescending(d => d.Count)
            .Take(5)
            .ToListAsync();
    }

    public async Task<IList<StatBucketDto>> GetPopularDecades()
    {
        var decadeGroups = await context.SeriesMetadata
            .Where(sm => sm.ReleaseYear != 0)
            .GroupBy(sm => (sm.ReleaseYear / 10) * 10) // Floor to decade
            .Select(g => new
            {
                Decade = g.Key,
                Count = g.Count()
            })
            .ToListAsync();

        var totalCount = decadeGroups.Sum(d => d.Count);

        return decadeGroups
            .OrderByDescending(d => d.Decade)
            .Select(d => new StatBucketDto
            {
                RangeStart = d.Decade,
                RangeEnd = d.Decade + 9,
                Count = d.Count,
                Percentage = totalCount > 0
                    ? Math.Round((decimal)d.Count / totalCount * 100, 2)
                    : 0
            })
            .ToList();
    }

    public async Task<IList<StatCount<LibraryDto>>> GetPopularLibraries()
    {
        var counts = await context.AppUserProgresses
            .Where(p => p.LibraryId > 0)
            .GetTopCounts(p => p.LibraryId, take: 5);

        var libraries = await context.Library
            .Where(l => counts.Select(c => c.Id).Contains(l.Id))
            .ProjectTo<LibraryDto>(mapper.ConfigurationProvider)
            .ToDictionaryAsync(l => l.Id);

        return counts
            .Where(c => libraries.ContainsKey(c.Id))
            .Select(lc => new StatCount<LibraryDto>
            {
                Value = libraries[lc.Id],
                Count = lc.Count
            })
            .ToList();
    }

    public async Task<IList<StatCount<SeriesDto>>> GetPopularSeries()
    {
        var counts = await context.AppUserProgresses
            .GetTopCounts(p => p.SeriesId, take: 5);

        if (counts.Count == 0) return [];


        var series = await context.Series
            .Where(s => counts.Select(c => c.Id).Contains(s.Id))
            .ProjectTo<SeriesDto>(mapper.ConfigurationProvider)
            .ToDictionaryAsync(s => s.Id);

        return counts
            .Where(c => series.ContainsKey(c.Id))
            .Select(sc => new StatCount<SeriesDto>
            {
                Value = series[sc.Id],
                Count = sc.Count
            })
            .ToList();
    }

    public async Task<IList<StatCount<ReadingListDto>>> GetPopularReadingList(int take = 5)
    {
        var readingListChapterCounts = await context.ReadingList
            .Where(rl => rl.Promoted)
            .Select(rl => new
            {
                ReadingListId = rl.Id,
                TotalChapters = rl.Items.Count
            })
            .Where(x => x.TotalChapters > 0)
            .ToDictionaryAsync(x => x.ReadingListId, x => x.TotalChapters);

        if (readingListChapterCounts.Count == 0) return [];

        var userReadCounts = await context.ReadingListItem
            .Where(rli => readingListChapterCounts.Keys.Contains(rli.ReadingListId))
            .Join(context.AppUserProgresses,
                rli => rli.ChapterId,
                p => p.ChapterId,
                (rli, p) => new { rli.ReadingListId, p.AppUserId, p.ChapterId, p.PagesRead })
            .Join(context.Chapter,
                x => x.ChapterId,
                c => c.Id,
                (x, c) => new { x.ReadingListId, x.AppUserId, x.ChapterId, x.PagesRead, c.Pages })
            .Where(x => x.PagesRead >= x.Pages)
            .GroupBy(x => new { x.ReadingListId, x.AppUserId })
            .Select(g => new UserReadCount(
                g.Key.ReadingListId,
                g.Key.AppUserId,
                g.Select(x => x.ChapterId).Distinct().Count()))
            .ToListAsync();

        if (userReadCounts.Count == 0) return [];

        var counts = RankReadingLists(userReadCounts, readingListChapterCounts, take);

        if (counts.Count == 0) return [];

        var readingListIds = counts.Select(c => c.ReadingListId).ToList();
        var readingLists = await context.ReadingList
            .Where(rl => readingListIds.Contains(rl.Id))
            .ProjectTo<ReadingListDto>(mapper.ConfigurationProvider)
            .ToDictionaryAsync(rl => rl.Id);

        return counts
            .Where(c => readingLists.ContainsKey(c.ReadingListId))
            .Select(c => new StatCount<ReadingListDto>
            {
                Value = readingLists[c.ReadingListId],
                Count = c.Count
            })
            .ToList();
    }

    private static List<(int ReadingListId, int Count)> RankReadingLists(
        IReadOnlyList<UserReadCount> userReadCounts,
        Dictionary<int, int> readingListChapterCounts,
        int take)
    {
        double[] thresholds = [0.5, 0.25, 0.0];

        foreach (var threshold in thresholds)
        {
            var counts = userReadCounts
                .Where(x => readingListChapterCounts.TryGetValue(x.ReadingListId, out var total)
                            && x.ChaptersRead >= Math.Ceiling(total * threshold))
                .GroupBy(x => x.ReadingListId)
                .Select(g => (ReadingListId: g.Key, Count: g.Count()))
                .OrderByDescending(x => x.Count)
                .Take(take)
                .ToList();

            if (counts.Count >= take || threshold == 0.0)
            {
                return counts;
            }
        }

        return [];
    }

    /// <summary>
    /// Top 5 genres where there is some reading activity
    /// </summary>
    /// <remarks>Since most users only tag the Series level metadata, this will only check against Series. Will count series * totalReads of series</remarks>
    /// <returns></returns>
    public async Task<IList<StatCount<GenreTagDto>>> GetPopularGenres()
    {
        var counts = await context.AppUserProgresses
            .GetTopCounts(p => p.SeriesId);

        if (counts.Count == 0) return [];

        var countDict = counts.ToDictionary(c => c.Id, c => c.Count);

        var genreStats = await context.Genre
            .SelectMany(g => g.SeriesMetadatas, (genre, sm) => new
            {
                Genre = genre,
                sm.SeriesId
            })
            .Where(x => countDict.Keys.Contains(x.SeriesId))
            .ToListAsync();

        return genreStats
            .GroupBy(x => x.Genre)
            .Select(g => new StatCount<GenreTagDto>
            {
                Value = new GenreTagDto
                {
                    Id = g.Key.Id,
                    Title = g.Key.Title
                },
                Count = g.Sum(x => countDict.GetValueOrDefault(x.SeriesId, 0))
            })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToList();
    }

    public async Task<IList<StatCount<TagDto>>> GetPopularTags()
    {
        var counts = await context.AppUserProgresses
            .GetTopCounts(p => p.SeriesId);

        if (counts.Count == 0)
            return [];

        var countDict = counts.ToDictionary(c => c.Id, c => c.Count);

        var genreStats = await context.Tag
            .SelectMany(g => g.SeriesMetadatas, (tag, sm) => new
            {
                Tag = tag,
                sm.SeriesId
            })
            .Where(x => countDict.Keys.Contains(x.SeriesId))
            .ToListAsync();

        return genreStats
            .GroupBy(x => x.Tag)
            .Select(g => new StatCount<TagDto>
            {
                Value = new TagDto
                {
                    Id = g.Key.Id,
                    Title = g.Key.Title
                },
                Count = g.Sum(x => countDict.GetValueOrDefault(x.SeriesId, 0))
            })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToList();
    }

    public async Task<IList<StatCount<PersonDto>>> GetPopularPerson(PersonRole role)
    {
        var counts = await context.AppUserProgresses
            .GetTopCounts(p => p.SeriesId);

        if (counts.Count == 0) return [];

        var countDict = counts.ToDictionary(c => c.Id, c => c.Count);

        var authorStats = await context.SeriesMetadataPeople
            .Where(smp => smp.Role == role)
            .Where(smp => countDict.Keys.Contains(smp.SeriesMetadata.SeriesId))
            .Select(smp => new
            {
                smp.Person,
                smp.SeriesMetadata.SeriesId
            })
            .ToListAsync();

        return authorStats
            .GroupBy(x => x.Person)
            .Select(g => new StatCount<PersonDto>
            {
                Value = new PersonDto
                {
                    Id = g.Key.Id,
                    Name = g.Key.Name,
                    CoverImage = g.Key.CoverImage,
                    PrimaryColor = g.Key.PrimaryColor,
                    SecondaryColor = g.Key.SecondaryColor,
                    Description = g.Key.Description
                },
                Count = g.Sum(x => countDict.GetValueOrDefault(x.SeriesId, 0))
            })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToList();
    }



    public async Task<IEnumerable<StatCount<PublicationStatus>>> GetPublicationCount()
    {
        return await context.SeriesMetadata
            .AsSplitQuery()
            .GroupBy(sm => sm.PublicationStatus)
            .Select(sm => new StatCount<PublicationStatus>
            {
                Value = sm.Key,
                Count = context.SeriesMetadata.Where(sm2 => sm2.PublicationStatus == sm.Key).Distinct().Count()
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<StatCount<MangaFormat>>> GetMangaFormatCount()
    {
        return await context.MangaFile
            .AsSplitQuery()
            .GroupBy(sm => sm.Format)
            .Select(mf => new StatCount<MangaFormat>
            {
                Value = mf.Key,
                Count = context.MangaFile.Where(mf2 => mf2.Format == mf.Key).Distinct().Count()
            })
            .ToListAsync();
    }

    public async Task<ServerStatisticsDto> GetServerStatistics()
    {
        var counts = await context.Chapter
            .Select(_ => new
            {
                Chapters = context.Chapter.Count(),
                Series = context.Series.Count(),
                Files = context.MangaFile.Count(),
                Genres = context.Genre.Count(),
                People = context.Person.Select(p => p.NormalizedName).Distinct().Count(),
                Tags = context.Tag.Count(),
                Volumes = context.Volume.Count(v => Math.Abs(v.MinNumber - Parser.LooseLeafVolumeNumber) > 0.001f),
                TotalBytes = context.MangaFile.Sum(m => m.Bytes)
            })
            .FirstOrDefaultAsync();

        if (counts == null) return new ServerStatisticsDto();

        var totalReadingHours = await context.AppUserReadingSessionActivityData
            .Where(a => a.EndTimeUtc != null)
            .Select(a => new { a.StartTimeUtc, EndTimeUtc = a.EndTimeUtc!.Value })
            .ToListAsync()
            .ContinueWith(t => t.Result.Sum(a => (a.EndTimeUtc - a.StartTimeUtc).TotalHours));

        return new ServerStatisticsDto
        {
            ChapterCount = counts.Chapters,
            SeriesCount = counts.Series,
            TotalFiles = counts.Files,
            TotalGenres = counts.Genres,
            TotalPeople = counts.People,
            TotalSize = counts.TotalBytes,
            TotalTags = counts.Tags,
            VolumeCount = counts.Volumes,
            TotalReadingTime = (long) totalReadingHours
        };
    }

    public async Task<FileExtensionBreakdownDto> GetFileBreakdown()
    {
        return new FileExtensionBreakdownDto()
        {
            FileBreakdown = await context.MangaFile
                .AsSplitQuery()
                .AsNoTracking()
                .GroupBy(sm => sm.Extension)
                .Select(mf => new FileExtensionDto()
                {
                    Extension = mf.Key,
                    Format =context.MangaFile.Where(mf2 => mf2.Extension == mf.Key).Select(mf2 => mf2.Format).Single(),
                    TotalSize = context.MangaFile.Where(mf2 => mf2.Extension == mf.Key).Distinct().Sum(mf2 => mf2.Bytes),
                    TotalFiles = context.MangaFile.Where(mf2 => mf2.Extension == mf.Key).Distinct().Count()
                })
                .OrderBy(d => d.TotalFiles)
                .ToListAsync(),
            TotalFileSize = await context.MangaFile
                .AsNoTracking()
                .AsSplitQuery()
                .SumAsync(f => f.Bytes)
        };
    }

    public async Task<IEnumerable<ReadHistoryEvent>> GetReadingHistory(int userId)
    {
        return await context.AppUserProgresses
            .Where(u => u.AppUserId == userId)
            .AsNoTracking()
            .AsSplitQuery()
            .Select(u => new ReadHistoryEvent
            {
                UserId = u.AppUserId,
                UserName = context.AppUser.Single(u2 => u2.Id == userId).UserName,
                SeriesName = context.Series.Single(s => s.Id == u.SeriesId).Name,
                SeriesId = u.SeriesId,
                LibraryId = u.LibraryId,
                ReadDate = u.LastModified,
                ReadDateUtc = u.LastModifiedUtc,
                ChapterId = u.ChapterId,
                ChapterNumber = context.Chapter.Single(c => c.Id == u.ChapterId).MinNumber
            })
            .OrderByDescending(d => d.ReadDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StatCountWithFormat<DateTime>>> ReadCountByDay(int userId = 0, int days = 0)
    {
        var query = context.AppUserProgresses
            .AsSplitQuery()
            .AsNoTracking()
            .Join(context.Chapter, appUserProgresses => appUserProgresses.ChapterId, chapter => chapter.Id,
                (appUserProgresses, chapter) => new {appUserProgresses, chapter})
            .Join(context.Volume, x => x.chapter.VolumeId, volume => volume.Id,
                (x, volume) => new {x.appUserProgresses, x.chapter, volume})
            .Join(context.Series, x => x.appUserProgresses.SeriesId, series => series.Id,
                (x, series) => new {x.appUserProgresses, x.chapter, x.volume, series})
            .WhereIf(userId > 0, x => x.appUserProgresses.AppUserId == userId)
            .WhereIf(days > 0, x => x.appUserProgresses.LastModified >= DateTime.Now.AddDays(days * -1));


        var results = await query.GroupBy(x => new
            {
                Day = x.appUserProgresses.LastModified.Date,
                x.series.Format,
            })
            .Select(g => new StatCountWithFormat<DateTime>
            {
                Value = g.Key.Day,
                Format = g.Key.Format,
                Count = (long) g.Sum(x =>
                    x.chapter.AvgHoursToRead * (x.appUserProgresses.PagesRead / (1.0f * x.chapter.Pages)))
            })
            .OrderBy(d => d.Value)
            .ToListAsync();

        if (results.Count > 0)
        {
            var minDay = results.Min(d => d.Value);
            for (var date = minDay; date < DateTime.Now; date = date.AddDays(1))
            {
                var resultsForDay = results.Where(d => d.Value == date).ToList();
                if (resultsForDay.Count > 0)
                {
                    // Add in types that aren't there (there is a bug in UI library that will cause dates to get out of order)
                    var existingFormats = resultsForDay.Select(r => r.Format).Distinct();
                    foreach (var format in Enum.GetValues(typeof(MangaFormat)).Cast<MangaFormat>().Where(f => f != MangaFormat.Unknown && !existingFormats.Contains(f)))
                    {
                        results.Add(new StatCountWithFormat<DateTime>()
                        {
                            Format = format,
                            Value = date,
                            Count = 0
                        });
                    }
                    continue;
                }
                results.Add(new StatCountWithFormat<DateTime>()
                {
                    Format = MangaFormat.Archive,
                    Value = date,
                    Count = 0
                });
                results.Add(new StatCountWithFormat<DateTime>()
                {
                    Format = MangaFormat.Epub,
                    Value = date,
                    Count = 0
                });
                results.Add(new StatCountWithFormat<DateTime>()
                {
                    Format = MangaFormat.Pdf,
                    Value = date,
                    Count = 0
                });
                results.Add(new StatCountWithFormat<DateTime>()
                {
                    Format = MangaFormat.Image,
                    Value = date,
                    Count = 0
                });
            }
        }

        return results.OrderBy(r => r.Value);
    }

    public async Task<IEnumerable<StatCountWithFormat<DateTime>>> ReadCounts(StatsFilterDto filter, int userId = 0)
    {
        var startDate = filter.StartDate?.ToUniversalTime() ?? DateTime.MinValue;
        var endDate = filter.EndDate?.ToUniversalTime() ?? DateTime.UtcNow;

        var results = await context.AppUserReadingSessionActivityData
            .AsNoTracking()
            .Where(a => a.StartTimeUtc >= startDate && a.StartTimeUtc <= endDate)
            .WhereIf(userId > 0, a => a.ReadingSession.AppUserId == userId)
            .WhereIf(filter.Libraries is { Count: > 0 }, a => filter.Libraries.Contains(a.LibraryId))
            .GroupBy(a => new { Day = a.StartTimeUtc.Date, a.Format })
            .Select(g => new StatCountWithFormat<DateTime>
            {
                Value = g.Key.Day,
                Format = g.Key.Format,
                Count = (long)g.Sum(a =>
                    (double)(a.EndTimeUtc!.Value.Ticks - a.StartTimeUtc.Ticks) / TimeSpan.TicksPerMinute)
            })
            .OrderBy(d => d.Value)
            .ToListAsync();

        FillMissingDaysAndFormats(results, startDate, endDate);

        return results.OrderBy(r => r.Value);
    }

    private static void FillMissingDaysAndFormats(List<StatCountWithFormat<DateTime>> results, DateTime startDate, DateTime endDate)
    {
        if (results.Count == 0)
            return;

        var validFormats = Enum.GetValues<MangaFormat>()
            .Where(f => f != MangaFormat.Unknown)
            .ToArray();

        var minDay = results.Min(d => d.Value);
        var effectiveStart = minDay > startDate.Date ? minDay : startDate.Date;
        var effectiveEnd = endDate.Date < DateTime.UtcNow.Date ? endDate.Date : DateTime.UtcNow.Date;

        var existingEntries = results
            .Select(r => (r.Value, r.Format))
            .ToHashSet();

        for (var date = effectiveStart; date <= effectiveEnd; date = date.AddDays(1))
        {
            foreach (var format in validFormats)
            {
                if (existingEntries.Contains((date, format)))
                    continue;

                results.Add(new StatCountWithFormat<DateTime>
                {
                    Format = format,
                    Value = date,
                    Count = 0
                });
            }
        }
    }

    public async Task<IList<StatCount<DayOfWeek>>> GetDayBreakdown(int userId = 0)
    {
        return await context.AppUserReadingSessionActivityData
            .AsNoTracking()
            .WhereIf(userId > 0, a => a.ReadingSession.AppUserId == userId)
            .GroupBy(a => a.StartTimeUtc.DayOfWeek)
            .OrderBy(g => g.Key)
            .Select(g => new StatCount<DayOfWeek>
            {
                Value = g.Key,
                Count = g.Count()
            })
            .ToListAsync();
    }

    /// <summary>
    /// Return a list of pages read per year for the given userId
    /// </summary>
    public async Task<IList<StatCount<int>>> GetPagesReadCountByYear(int userId = 0)
    {
        return await context.AppUserReadingSessionActivityData
            .AsNoTracking()
            .WhereIf(userId > 0, a => a.ReadingSession.AppUserId == userId)
            .GroupBy(a => a.StartTimeUtc.Year)
            .OrderBy(g => g.Key)
            .Select(g => new StatCount<int>
            {
                Value = g.Key,
                Count = g.Sum(a => a.PagesRead)
            })
            .ToListAsync();
    }

    /// <summary>
    /// Return a list of words read per year for the given userId
    /// </summary>
    public async Task<IList<StatCount<int>>> GetWordsReadCountByYear(int userId = 0)
    {
        return await context.AppUserReadingSessionActivityData
            .AsNoTracking()
            .Where(a => a.WordsRead > 0)
            .WhereIf(userId > 0, a => a.ReadingSession.AppUserId == userId)
            .GroupBy(a => a.StartTimeUtc.Year)
            .OrderBy(g => g.Key)
            .Select(g => new StatCount<int>
            {
                Value = g.Key,
                Count = g.Sum(a => a.WordsRead)
            })
            .ToListAsync();
    }

    /// <summary>
    /// Updates the ServerStatistics table for the current year
    /// </summary>
    /// <remarks>This commits</remarks>
    /// <returns></returns>
    public async Task UpdateServerStatistics()
    {
        var year = DateTime.Today.Year;

        var existingRecord = await context.ServerStatistics.SingleOrDefaultAsync(s => s.Year == year) ?? new ServerStatistics();

        existingRecord.Year = year;
        existingRecord.ChapterCount = await context.Chapter.CountAsync();
        existingRecord.VolumeCount = await context.Volume.CountAsync();
        existingRecord.FileCount = await context.MangaFile.CountAsync();
        existingRecord.SeriesCount = await context.Series.CountAsync();
        existingRecord.UserCount = await context.Users.CountAsync();
        existingRecord.GenreCount = await context.Genre.CountAsync();
        existingRecord.TagCount = await context.Tag.CountAsync();
        existingRecord.PersonCount =  context.Person
            .AsSplitQuery()
            .AsEnumerable()
            .GroupBy(sm => sm.NormalizedName)
            .Select(sm => sm.Key)
            .Distinct()
            .Count();

        context.ServerStatistics.Attach(existingRecord);
        if (existingRecord.Id > 0)
        {
            context.Entry(existingRecord).State = EntityState.Modified;
        }
        await unitOfWork.CommitAsync();
    }

    public async Task<long> TimeSpentReadingForUsersAsync(IList<int> userIds, IList<int> libraryIds)
    {
        var query = context.AppUserProgresses
            .WhereIf(userIds.Any(), p => userIds.Contains(p.AppUserId))
            .WhereIf(libraryIds.Any(), p => libraryIds.Contains(p.LibraryId))
            .AsSplitQuery();

        return (long) Math.Round(await query
            .Join(context.Chapter,
                p => p.ChapterId,
                c => c.Id,
                (progress, chapter) => new {chapter, progress})
            .Where(p => p.chapter.AvgHoursToRead > 0)
            .SumAsync(p =>
                p.chapter.AvgHoursToRead * (p.progress.PagesRead / (1.0f * p.chapter.Pages))));
    }

    public async Task<IEnumerable<FileExtensionExportDto>> GetFilesByExtension(string fileExtension)
    {
        var query = context.MangaFile
            .Where(f => f.Extension == fileExtension)
            .ProjectTo<FileExtensionExportDto>(mapper.ConfigurationProvider)
            .OrderBy(f => f.FilePath);

        return await query.ToListAsync();
    }

    public async Task<DeviceClientBreakdownDto> GetClientTypeBreakdown(DateTime fromDateUtc)
    {
        var devices = await context.ClientDevice
            .Where(d => d.IsActive && d.LastSeenUtc >= fromDateUtc)
            .Select(d => d.CurrentClientInfo.ClientType)
            .ToListAsync();

        var grouped = devices
            .GroupBy(clientType => clientType)
            .Select(g => new StatCount<ClientDeviceType>
            {
                Value = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(s => s.Count)
            .ToList();

        return new DeviceClientBreakdownDto
        {
            Records = grouped,
            TotalCount = devices.Count
        };
    }


    public async Task<IList<StatCount<string>>> GetDeviceTypeCounts(DateTime fromDateUtc)
    {
        var devices = await context.ClientDevice
            .Where(d => d.IsActive && d.LastSeenUtc >= fromDateUtc)
            .Select(d => d.CurrentClientInfo.DeviceType)
            .ToListAsync();

        // Define the expected device types
        var knownDeviceTypes = new[] { "mobile", "desktop", "tablet" };

        var grouped = devices
            .Where(deviceType => !string.IsNullOrEmpty(deviceType))
            .GroupBy(deviceType => deviceType!.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => (long)g.Count());

        // Ensure all known types are present, even with 0 count
        var result = knownDeviceTypes
            .Select(deviceType => new StatCount<string>
            {
                Value = deviceType,
                Count = grouped.GetValueOrDefault(deviceType, 0)
            })
            .OrderByDescending(s => s.Count)
            .ToList();

        return result;
    }

    public async Task<ReadingActivityGraphDto> GetReadingActivityGraphData(StatsFilterDto filter, int userId, int year,
        int requestingUserId)
    {
        var socialPreferences = await unitOfWork.UserRepository.GetSocialPreferencesForUser(userId);
        var requestingUser = await unitOfWork.UserRepository.GetUserByIdAsync(requestingUserId);

        var startDate = filter.StartDate?.ToUniversalTime() ?? DateTime.MinValue;
        var endDate = filter.EndDate?.ToUniversalTime() ?? DateTime.UtcNow;

        var sessionActivityData = await context.AppUserReadingSession
            .Where(s => s.AppUserId == userId)
            .Where(s => s.StartTimeUtc >= startDate && s.EndTimeUtc <= endDate)
            .Where(s => s.EndTimeUtc != null)
            .Join(
                context.AppUserReadingSessionActivityData
                    .ApplyStatsFilter(filter, userId, socialPreferences, requestingUser, false, true),
                session => session.Id,
                activity => activity.AppUserReadingSessionId,
                (session, activity) => new
                {
                    SessionDate = session.StartTimeUtc.Date,
                    SessionId = session.Id,
                    SessionStartUtc = session.StartTimeUtc,
                    SessionEndUtc = session.EndTimeUtc!.Value,
                    activity.ChapterId,
                    activity.PagesRead,
                    activity.WordsRead,
                    activity.TotalPages
                })
            .ToListAsync();

        var result = new ReadingActivityGraphDto();

        if (sessionActivityData.Count == 0) return result;

        var dailyStats = sessionActivityData
            .GroupBy(x => x.SessionDate)
            .Select(dayGroup => new
            {
                Date = dayGroup.Key,
                DateKey = dayGroup.Key.ToString("yyyy-MM-dd"),
                // Sum durations across all sessions for this day
                TotalTimeReadingSeconds = dayGroup
                    .GroupBy(x => x.SessionId)
                    .Sum(sessionGroup =>
                        (int) (sessionGroup.First().SessionEndUtc - sessionGroup.First().SessionStartUtc).TotalSeconds),
                // Sum pages/words across all activities
                TotalPages = dayGroup.Sum(x => x.PagesRead),
                TotalWords = dayGroup.Sum(x => x.WordsRead),
                // Count distinct chapters that were fully read per day
                TotalChaptersFullyRead = dayGroup
                    .Where(x => x.PagesRead > 0 && x.TotalPages > 0 && x.PagesRead >= x.TotalPages)
                    .Select(x => x.ChapterId)
                    .Distinct()
                    .Count()
            })
            .ToList();

        foreach (var stat in dailyStats)
        {
            result[stat.DateKey] = new ReadingActivityGraphEntryDto
            {
                Date = stat.Date,
                TotalTimeReadingSeconds = stat.TotalTimeReadingSeconds,
                TotalPages = stat.TotalPages,
                TotalWords = stat.TotalWords,
                TotalChaptersFullyRead = stat.TotalChaptersFullyRead
            };

            if (result.Count <= 0) return result;

            var currentDate = startDate;
            while (currentDate.Year == year)
            {
                var dateKey = currentDate.ToString("yyyy-MM-dd");
                if (!result.ContainsKey(dateKey))
                {
                    result[dateKey] = new ReadingActivityGraphEntryDto
                    {
                        Date = currentDate,
                        TotalTimeReadingSeconds = 0,
                        TotalPages = 0,
                        TotalWords = 0,
                        TotalChaptersFullyRead = 0
                    };
                }

                currentDate = currentDate.AddDays(1);
            }
        }

        return result;
    }

    public async Task<ReadingPaceDto> GetReadingPaceForUser(StatsFilterDto filter, int userId, int year, bool booksOnly, int requestingUserId)
    {
        var socialPreferences = await unitOfWork.UserRepository.GetSocialPreferencesForUser(userId);
        var requestingUser = await unitOfWork.UserRepository.GetUserByIdAsync(requestingUserId);

        var firstProgress = await unitOfWork.AppUserProgressRepository.GetFirstProgressForUser(userId);
        if (firstProgress == null)
        {
            return new ReadingPaceDto();
        }

        filter.StartDate ??= firstProgress;
        filter.StartDate = filter.StartDate > firstProgress ? filter.StartDate : firstProgress;
        filter.EndDate ??= DateTime.UtcNow;
        filter.EndDate = filter.EndDate < DateTime.UtcNow ? filter.EndDate : DateTime.UtcNow;

        var activities = await context.AppUserReadingSessionActivityData
            .ApplyStatsFilter(filter, userId, socialPreferences, requestingUser, isAggregate: true, onlyCompleted: false)
            .Select(a => new
            {
                a.PagesRead,
                a.WordsRead,
                a.ChapterId,
                a.SeriesId,
                SeriesFormat = a.Series.Format,
                SessionStart = a.ReadingSession.StartTimeUtc,
                SessionEnd = a.ReadingSession.EndTimeUtc,
                Finished = a.EndPage >= a.Chapter.Pages,
            })
            .WhereIf(booksOnly, d => d.SeriesFormat == MangaFormat.Pdf || d.SeriesFormat == MangaFormat.Epub)
            .WhereIf(!booksOnly, d => d.SeriesFormat != MangaFormat.Pdf && d.SeriesFormat != MangaFormat.Epub)
            .ToListAsync();

        var sessionDurations = activities
            .Where(a => a.SessionEnd.HasValue)
            .GroupBy(a => new { a.SessionStart, a.SessionEnd })
            .Sum(g => (g.Key.SessionEnd!.Value - g.Key.SessionStart).TotalHours);

        var booksRead = new HashSet<int>();
        var comicsRead = new HashSet<int>();
        var pagesRead = 0;
        var wordsRead = 0;

        foreach (var activity in activities)
        {
            pagesRead += activity.PagesRead;
            wordsRead += activity.WordsRead;

            if (!activity.Finished) continue;

            if (activity.SeriesFormat is MangaFormat.Epub or MangaFormat.Pdf)
                booksRead.Add(activity.ChapterId);
            else
                comicsRead.Add(activity.ChapterId);
        }

        var timeSpan = (filter.EndDate - filter.StartDate).Value;
        var daysInRange = (int)timeSpan.TotalDays + 1;

        return new ReadingPaceDto
        {
            HoursRead = (int)Math.Round(sessionDurations),
            PagesRead = pagesRead,
            WordsRead = wordsRead,
            BooksRead = booksRead.Count,
            ComicsRead = comicsRead.Count,
            DaysInRange = daysInRange
        };
    }

    public async Task<BreakDownDto<string>> GetGenreBreakdownForUser(StatsFilterDto filter, int userId, int requestingUserId)
    {
        var socialPreferences = await unitOfWork.UserRepository.GetSocialPreferencesForUser(userId);
        var requestingUser = await unitOfWork.UserRepository.GetUserByIdAsync(requestingUserId);

        var readsPerGenre = await context.AppUserReadingSessionActivityData
            .ApplyStatsFilter(filter, userId, socialPreferences, requestingUser)
            .GroupBy(d => d.SeriesId)
            .Select(d => new
            {
                SeriesId = d.Key,
                TotalReads = d.Count(),
            })
            .Join(context.SeriesMetadata, x => x.SeriesId, sm => sm.SeriesId, (x, sm) => new
            {
                x.SeriesId,
                x.TotalReads,
                SeriesMetadataId = sm.Id,
            })
            .Join(context.GenreSeriesMetadata, x => x.SeriesMetadataId, gsm => gsm.SeriesMetadatasId, (x, gsm) => new
            {
                x.SeriesId,
                x.TotalReads,
                gsm.GenresId,
            })
            .Join(context.Genre, x => x.GenresId, g => g.Id, (x, g) => new
            {
                x.SeriesId,
                x.TotalReads,
                Genre = g,
            })
            .GroupBy(x => new
            {
                x.Genre.Id,
                x.Genre.Title,
            })
            .Select(g => new StatCount<string>
            {
                Value = g.Key.Title,
                Count = g.Select(x => x.SeriesId).Distinct().Count(),
            })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync();

        var totalMissingData = await context.AppUserReadingSessionActivityData
            .ApplyStatsFilter(filter, userId, socialPreferences, requestingUser)
            .Select(p => p.SeriesId)
            .Distinct()
            .Join(context.SeriesMetadata, p => p, sm => sm.SeriesId, (g, m) => m.Genres)
            .CountAsync(g => !g.Any());

        var totalReads = await context.AppUserReadingSessionActivityData
            .ApplyStatsFilter(filter, userId, socialPreferences, requestingUser)
            .Select(p => p.SeriesId)
            .Distinct()
            .CountAsync();

        var totalReadGenres = await context.AppUserReadingSessionActivityData
            .ApplyStatsFilter(filter, userId, socialPreferences, requestingUser)
            .Join(context.Chapter, p => p.ChapterId, c => c.Id, (p, c) => c.Genres)
            .SelectMany(g => g.Select(gg => gg.NormalizedTitle))
            .Distinct()
            .CountAsync();

        return new BreakDownDto<string>()
        {
            Data = readsPerGenre,
            Missing = totalMissingData,
            Total = totalReads,
            TotalOptions = totalReadGenres,
        };

    }

    public async Task<BreakDownDto<string>> GetTagBreakdownForUser(StatsFilterDto filter, int userId, int requestingUserId)
    {
        var socialPreferences = await unitOfWork.UserRepository.GetSocialPreferencesForUser(userId);
        var requestingUser = await unitOfWork.UserRepository.GetUserByIdAsync(requestingUserId);

        var readsPerTagTask =  context.AppUserReadingSessionActivityData
            .ApplyStatsFilter(filter, userId, socialPreferences, requestingUser)
            .GroupBy(d => d.SeriesId)
            .Select(d => new
            {
                SeriesId = d.Key,
                TotalReads = d.Count(),
            })
            .Join(context.SeriesMetadata, x => x.SeriesId, sm => sm.SeriesId, (x, sm) => new
            {
                x.SeriesId,
                x.TotalReads,
                SeriesMetadataId = sm.Id,
            })
            .Join(context.SeriesMetadataTag, x => x.SeriesMetadataId, smt => smt.SeriesMetadatasId, (x, smt) => new
            {
                x.SeriesId,
                x.TotalReads,
                smt.TagsId,
            })
            .Join(context.Tag, x => x.TagsId, t => t.Id, (x, t) => new
            {
                x.SeriesId,
                x.TotalReads,
                Tag = t,
            })
            .GroupBy(x => new
            {
                x.Tag.Id,
                x.Tag.Title,
            })
            .Select(g => new StatCount<string>
            {
                Value = g.Key.Title,
                Count = g.Select(x => x.SeriesId).Distinct().Count(),
            })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync();

        var totalMissingDataTask =  context.AppUserReadingSessionActivityData
            .ApplyStatsFilter(filter, userId, socialPreferences, requestingUser)
            .Select(p => p.SeriesId)
            .Distinct()
            .Join(context.SeriesMetadata, p => p, sm => sm.SeriesId, (g, m) => m.Tags)
            .CountAsync(g => !g.Any());

        var totalReadsTask =  context.AppUserReadingSessionActivityData
            .ApplyStatsFilter(filter, userId, socialPreferences, requestingUser)
            .Select(p => p.SeriesId)
            .Distinct()
            .CountAsync();

        var totalReadTagsTask =  context.AppUserReadingSessionActivityData
            .ApplyStatsFilter(filter, userId, socialPreferences, requestingUser)
            .Join(context.Chapter, p => p.ChapterId, c => c.Id, (p, c) => c.Tags)
            .SelectMany(g => g.Select(gg => gg.NormalizedTitle))
            .Distinct()
            .CountAsync();

        await Task.WhenAll(readsPerTagTask, totalMissingDataTask, totalReadsTask, totalReadTagsTask);

        return new BreakDownDto<string>()
        {
            Data = await readsPerTagTask,
            Missing = await totalMissingDataTask,
            Total = await totalReadsTask,
            TotalOptions = await totalReadTagsTask,
        };
    }

    public async Task<SpreadStatsDto> GetPageSpreadForUser(StatsFilterDto filter, int userId, int requestingUserId)
    {
        var socialPreferences = await unitOfWork.UserRepository.GetSocialPreferencesForUser(userId);
        var requestingUser = await unitOfWork.UserRepository.GetUserByIdAsync(requestingUserId);

        var fullyReadChapters = await context.AppUserReadingSessionActivityData
            .ApplyStatsFilter(filter, userId, socialPreferences, requestingUser, isAggregate: true)
            .Join(
                context.Chapter,
                progress => progress.ChapterId,
                chapter => chapter.Id,
                (progress, chapter) => new { progress, chapter }
            )
            .Select(x => x.chapter.Pages)
            .ToListAsync();

        var totalCount = fullyReadChapters.Count;
        var highest = fullyReadChapters.MaxOrDefault(x => x, 0);

        if (highest == 0)
        {
            return new SpreadStatsDto()
            {
                Buckets = [],
                TotalCount = 0
            };
        }

        var magnitude = (int) Math.Floor(Math.Log10(highest));
        var bucketSize = (int) Math.Pow(10, magnitude - 1);

        var bucketCount = 8;
        var buckets = Enumerable.Range(0, bucketCount).Select(i =>
        {
            var isLastBucket = i + 1 == bucketCount;

            var start = i * bucketSize;
            var end = isLastBucket ? int.MaxValue : (i + 1) * bucketSize;

            var count = fullyReadChapters.Count(pages =>
                pages >= start &&
                (pages <= end)
            );

            return new StatBucketDto
            {
                RangeStart = start,
                RangeEnd = isLastBucket ? null : end,
                Count = count,
                Percentage = totalCount > 0 ? (decimal)count / totalCount * 100 : 0
            };
        }).ToList();

        return new SpreadStatsDto
        {
            Buckets = buckets,
            TotalCount = totalCount,
        };
    }

    public async Task<SpreadStatsDto> GetWordSpreadForUser(StatsFilterDto filter, int userId, int requestingUserId)
    {
        var socialPreferences = await unitOfWork.UserRepository.GetSocialPreferencesForUser(userId);
        var requestingUser = await unitOfWork.UserRepository.GetUserByIdAsync(requestingUserId);

        var wordsInFullyReadChapters = await context.AppUserReadingSessionActivityData
            .ApplyStatsFilter(filter, userId, socialPreferences, requestingUser, isAggregate: true)
            .Join(
                context.Chapter,
                progress => progress.ChapterId,
                chapter => chapter.Id,
                (progress, chapter) => new { progress, chapter }
            )
            .Where(x => x.chapter.WordCount > 0)
            .Select(x => x.chapter.WordCount)
            .ToListAsync();

        var totalCount = wordsInFullyReadChapters.Count;
        var highest = wordsInFullyReadChapters.MaxOrDefault(x => x, 0);

        if (highest == 0)
        {
            return new SpreadStatsDto()
            {
                Buckets = [],
                TotalCount = 0
            };
        }


        var magnitude = (int) Math.Floor(Math.Log10(highest));
        var bucketSize = (int) Math.Pow(10, magnitude - 1);

        var bucketCount = 8;
        var buckets = Enumerable.Range(0, bucketCount)
            .Select(i =>
            {
                var isLastBucket = i + 1 == bucketCount;

                var start = i * bucketSize;
                var end = isLastBucket ? int.MaxValue : (i + 1) * bucketSize;

                var count = wordsInFullyReadChapters
                    .Count(v => v >= start && v < end);

                return new StatBucketDto
                {
                    RangeStart = start,
                    RangeEnd = isLastBucket ? null : end,
                    Count = count,
                    Percentage = totalCount > 0 ? (decimal)count / totalCount * 100 : 0,
                };
            })
            .ToList();

        return new SpreadStatsDto
        {
            Buckets = buckets,
            TotalCount = totalCount,
        };

    }

    public async Task<ReadTimeByHourDto?> GetTimeReadingByHour(StatsFilterDto filter, int userId, int requestingUserId)
    {
        var socialPreferences = await unitOfWork.UserRepository.GetSocialPreferencesForUser(userId);
        var requestingUser = await unitOfWork.UserRepository.GetUserByIdAsync(requestingUserId);

        var sessionRecordedSince = await unitOfWork.DataContext.ManualMigrationHistory
            .FirstOrDefaultAsync(mm => mm.Name == MigrateProgressToReadingSessions.Name);

        if (sessionRecordedSince == null)
        {
            logger.LogWarning("{Migration} never happened? Cannot compute time by hour", MigrateProgressToReadingSessions.Name);
            return null;
        }

        filter.StartDate ??= sessionRecordedSince.RanAt;
        filter.StartDate = filter.StartDate < sessionRecordedSince.RanAt ? sessionRecordedSince.RanAt : filter.StartDate;

        var sessions = await context.AppUserReadingSessionActivityData
            .ApplyStatsFilter(filter, userId, socialPreferences, requestingUser, isAggregate: true)
            .Where(session => session.ReadingSession.CreatedUtc > sessionRecordedSince.RanAt)
            .ToListAsync();

        var hourStats = sessions
            .SelectMany(session =>
            {
                var hours = new List<(DateOnly day, int hour, TimeSpan timeSpent)>();
                var current = session.StartTime;

                while (current < session.EndTime)
                {
                    var hourEnd = current.AddHours(1);
                    var sessionEnd = session.EndTime ?? current;
                    var endOfPeriod = new[] { hourEnd, sessionEnd }.Min();

                    var timeSpent = endOfPeriod - current;
                    hours.Add((DateOnly.FromDateTime(current), current.Hour, timeSpent));

                    current = endOfPeriod;
                }

                return hours;
            })
            .GroupBy(x => new { x.day, x.hour })
            .Select(g => new
            {
                g.Key.day,
                g.Key.hour,
                totalTimeSpent = g.Sum(x => x.timeSpent.TotalMinutes)
            })
            .GroupBy(x => x.hour)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(x => x.totalTimeSpent) / g.Count()
            );

        var data = Enumerable.Range(0, 24)
            .Select(hour => new StatCount<int>
            {
                Value = hour,
                Count = (long) Math.Ceiling(hourStats.TryGetValue(hour, out var value) ? value : 0),
            })
            .ToList();

        return new ReadTimeByHourDto
        {
            DataSince = sessionRecordedSince.RanAt,
            Stats = data,
        };
    }

    public async Task<ProfileStatBarDto> GetUserStatBar(StatsFilterDto filter, int userId, int requestingUserId)
    {
        var socialPreferences = await unitOfWork.UserRepository.GetSocialPreferencesForUser(userId);
        var requestingUser = await unitOfWork.UserRepository.GetUserByIdAsync(requestingUserId);

        var chapterData = await context.AppUserReadingSessionActivityData
            .ApplyStatsFilter(filter, userId, socialPreferences, requestingUser, isAggregate: true, onlyCompleted: false)
            .Select(d => new
            {
                d.ChapterId,
                FormatType = d.Chapter.Files.First().Format,
                d.PagesRead,
                d.WordsRead,
                Finished = d.EndPage >= d.Chapter.Pages
            })
            .ToListAsync();

        // Early exit if no data
        if (chapterData.Count == 0)
        {
            // Still need reviews/ratings - run in parallel
            var (reviews, ratings) = await GetReviewsAndRatings(filter, userId, socialPreferences);
            return new ProfileStatBarDto
            {
                Reviews = reviews,
                Ratings = ratings
            };
        }

        // Group by ChapterId to deduplicate, then aggregate
        var byChapter = chapterData
            .GroupBy(x => x.ChapterId)
            .Select(g => new
            {
                ChapterId = g.Key,
                g.First().FormatType,
                PagesRead = g.Sum(x => x.PagesRead),
                WordsRead = g.Sum(x => x.WordsRead),
                Finished = g.Any(x => x.Finished)
            })
            .ToList();

        var chapterIds = byChapter
            .Select(x => x.ChapterId)
            .ToHashSet();

        var booksRead = 0;
        var comicsRead = 0;
        var pagesRead = 0L;
        var wordsRead = 0L;

        foreach (var ch in byChapter)
        {
            pagesRead += ch.PagesRead;
            wordsRead += ch.WordsRead;

            if (!ch.Finished) continue;

            switch (ch.FormatType)
            {
                case MangaFormat.Pdf or MangaFormat.Epub:
                    booksRead++;
                    break;
                case MangaFormat.Archive or MangaFormat.Image or MangaFormat.Unknown:
                    comicsRead++;
                    break;
            }
        }

        var authorsTask = GetAuthorsCount(chapterIds);
        var reviewsRatingsTask = GetReviewsAndRatings(filter, userId, socialPreferences);

        await Task.WhenAll(authorsTask, reviewsRatingsTask);

        var (reviewCount, ratingCount) = await reviewsRatingsTask;

        return new ProfileStatBarDto
        {
            BooksRead = booksRead,
            ComicsRead = comicsRead,
            PagesRead = (int)pagesRead,
            WordsRead = (int)wordsRead,
            AuthorsRead = await authorsTask,
            Reviews = reviewCount,
            Ratings = ratingCount
        };
    }

    public async Task<IList<MostActiveUserDto>> GetMostActiveUsers(StatsFilterDto filter)
    {
        var startDate = filter.StartDate?.ToUniversalTime() ?? DateTime.MinValue;
        var endDate = filter.EndDate?.ToUniversalTime() ?? DateTime.UtcNow;

        // Fetch activity data for all users in the time period
        var activityData = await context.AppUserReadingSessionActivityData
            .Between(a => a.StartTimeUtc, startDate, endDate)
            .Where(a => a.EndTimeUtc != null)
            .Select(a => new
            {
                a.ReadingSession.AppUserId,
                a.ChapterId,
                a.SeriesId,
                a.Chapter.Files.First().Format,
                a.StartTimeUtc,
                EndTimeUtc = a.EndTimeUtc!.Value
            })
            .ToListAsync();

        if (activityData.Count == 0) return [];

        // Group by user and calculate stats, take top 5 by hours
        var userStats = activityData
            .GroupBy(a => a.AppUserId)
            .Select(userGroup =>
            {
                var userId = userGroup.Key;

                var hoursRead = userGroup.Sum(a => (a.EndTimeUtc - a.StartTimeUtc).TotalHours);

                var bookChapters = userGroup
                    .Where(a => a.Format is MangaFormat.Epub or MangaFormat.Pdf)
                    .Select(a => a.ChapterId)
                    .Distinct()
                    .Count();

                var comicChapters = userGroup
                    .Where(a => a.Format is not MangaFormat.Epub and not MangaFormat.Pdf)
                    .Select(a => a.ChapterId)
                    .Distinct()
                    .Count();

                var seriesIds = userGroup
                    .Select(a => a.SeriesId)
                    .Distinct()
                    .ToList();

                return new
                {
                    UserId = userId,
                    HoursRead = hoursRead,
                    BooksRead = bookChapters,
                    ComicsRead = comicChapters,
                    SeriesIds = seriesIds
                };
            })
            .OrderByDescending(u => u.HoursRead)
            .Take(5)
            .ToList();

        if (userStats.Count == 0) return [];

        var userIds = userStats.Select(u => u.UserId).ToList();

        // Fetch user details
        var users = await context.AppUser
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName, u.CoverImage })
            .ToDictionaryAsync(u => u.Id);

        // Fetch TotalReads for each user's series
        var allSeriesIds = userStats
            .SelectMany(u => u.SeriesIds)
            .Distinct()
            .ToList();

        var progressData = await context.AppUserProgresses
            .Where(p => userIds.Contains(p.AppUserId) && allSeriesIds.Contains(p.SeriesId))
            .GroupBy(p => new { p.AppUserId, p.SeriesId })
            .Select(g => new
            {
                g.Key.AppUserId,
                g.Key.SeriesId,
                MinTotalReads = g.Min(p => p.TotalReads)
            })
            .ToListAsync();

        var progressLookup = progressData.ToLookup(p => p.AppUserId);

        // Fetch series for projection
        var seriesLookup = await context.Series
            .Where(s => allSeriesIds.Contains(s.Id))
            .ProjectTo<SeriesDto>(mapper.ConfigurationProvider)
            .ToDictionaryAsync(s => s.Id);

        var result = new List<MostActiveUserDto>();
        foreach (var stat in userStats)
        {
            if (!users.TryGetValue(stat.UserId, out var user))
                continue;

            var topSeries = progressLookup[stat.UserId]
                .Where(p => stat.SeriesIds.Contains(p.SeriesId))
                .OrderByDescending(p => p.MinTotalReads)
                .Take(5)
                .Select(p => seriesLookup.GetValueOrDefault(p.SeriesId))
                .Where(s => s != null)
                .Cast<SeriesDto>()
                .ToList();

            result.Add(new MostActiveUserDto
            {
                UserId = stat.UserId,
                Username = user.UserName ?? string.Empty,
                CoverImage = user.CoverImage,
                TimePeriodHours = (int)Math.Round(stat.HoursRead),
                TotalHours = (int)Math.Round(stat.HoursRead),
                TotalComics = stat.ComicsRead,
                TotalBooks = stat.BooksRead,
                TopSeries = topSeries
            });
        }

        return result;
    }

    public async Task<IList<StatCountWithFormat<DateTime>>> GetFilesAddedOverTime()
    {
        var results = await context.MangaFile
            .AsNoTracking()
            .GroupBy(f => new { Date = f.CreatedUtc.Date, f.Format })
            .Select(g => new StatCountWithFormat<DateTime>
            {
                Value = g.Key.Date,
                Count = g.Count(),
                Format = g.Key.Format
            })
            .OrderBy(d => d.Value)
            .ToListAsync();

        return results;
    }

    private async Task<int> GetAuthorsCount(HashSet<int> chapterIds)
    {
        if (chapterIds.Count == 0) return 0;

        // For large sets, batch to avoid SQLite parameter limits (max ~999)
        if (chapterIds.Count <= 500)
        {
            return await context.ChapterPeople
                .Where(cp => cp.Role == PersonRole.Writer && chapterIds.Contains(cp.ChapterId))
                .Select(cp => cp.PersonId)
                .Distinct()
                .CountAsync();
        }

        // Batch approach for large chapter sets
        var authorIds = new HashSet<int>();
        foreach (var batch in chapterIds.Chunk(500))
        {
            var batchSet = batch.ToHashSet();
            var batchAuthors = await context.ChapterPeople
                .Where(cp => cp.Role == PersonRole.Writer && batchSet.Contains(cp.ChapterId))
                .Select(cp => cp.PersonId)
                .ToListAsync();

            foreach (var id in batchAuthors)
                authorIds.Add(id);
        }
        return authorIds.Count;
    }

    private async Task<(int Reviews, int Ratings)> GetReviewsAndRatings(
        StatsFilterDto filter, int userId, AppUserSocialPreferences socialPreferences)
    {
        var baseQuery = BuildRatingQuery(filter, userId, socialPreferences);

        // Single query with conditional counting
        var counts = await baseQuery
            .GroupBy(r => 1)
            .Select(g => new
            {
                Reviews = g.Count(r => r.Review != null && r.Review != ""),
                Ratings = g.Count(r => r.HasBeenRated)
            })
            .FirstOrDefaultAsync();

        return counts != null ? (counts.Reviews, counts.Ratings) : (0, 0);
    }

    private IQueryable<AppUserRating> BuildRatingQuery(
        StatsFilterDto filter, int userId, AppUserSocialPreferences socialPreferences)
    {
        return context.AppUserRating
            .Where(r => r.AppUserId == userId)
            .WhereIf(filter.Libraries is { Count: > 0 },
                r => filter.Libraries!.Contains(r.Series.LibraryId))
            .WhereIf(filter.StartDate != null,
                r => r.CreatedUtc >= filter.StartDate!.Value.ToUniversalTime())
            .WhereIf(filter.EndDate != null,
                r => r.CreatedUtc <= filter.EndDate!.Value.ToUniversalTime())
            .WhereIf(socialPreferences.SocialLibraries.Count > 0,
                r => socialPreferences.SocialLibraries.Contains(r.Series.LibraryId))
            .WhereIf(socialPreferences.SocialMaxAgeRating != AgeRating.NotApplicable,
                r => (socialPreferences.SocialMaxAgeRating >= r.Series.Metadata.AgeRating &&
                      r.Series.Metadata.AgeRating != AgeRating.Unknown) ||
                     (socialPreferences.SocialIncludeUnknowns &&
                      r.Series.Metadata.AgeRating == AgeRating.Unknown));
    }

    public async Task<IList<StatCount<YearMonthGroupingDto>>> GetReadsPerMonth(StatsFilterDto filter, int userId, int requestingUserId)
    {
        var socialPreferences = await unitOfWork.UserRepository.GetSocialPreferencesForUser(userId);
        var requestingUser = await unitOfWork.UserRepository.GetUserByIdAsync(requestingUserId);

        return await context.AppUserReadingSessionActivityData
            .ApplyStatsFilter(filter, userId, socialPreferences, requestingUser, isAggregate: true)
            .GroupBy(s => new {s.ReadingSession.CreatedUtc.Year, s.ReadingSession.CreatedUtc.Month})
            .Select(g => new StatCount<YearMonthGroupingDto>()
            {
                Value = new YearMonthGroupingDto()
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                },
                Count = g.Count(),
            }).ToListAsync();
    }

    public async Task<IList<MostReadAuthorsDto>> GetMostReadAuthors(StatsFilterDto filter, int userId, int requestingUserId)
    {
        var socialPreferences = await unitOfWork.UserRepository.GetSocialPreferencesForUser(userId);
        var requestingUser = await unitOfWork.UserRepository.GetUserByIdAsync(requestingUserId);

        var res = await context.ChapterPeople
            .Where(cp => cp.Role == PersonRole.Writer)
            .Join(
                context.AppUserReadingSessionActivityData.ApplyStatsFilter(filter, userId, socialPreferences, requestingUser),
                cp => cp.ChapterId,
                d => d.ChapterId,
                (cp, data) => new { cp.PersonId, cp.ChapterId, cp.Person.Name }
            )
            .GroupBy(x => new { x.PersonId, x.Name })
            .Select(g => new
            {
                g.Key.PersonId,
                AuthorName = g.Key.Name,
                TotalChaptersRead = g.Select(x => x.ChapterId).Distinct().Count(),
                ChapterIds = g.Select(x => x.ChapterId).OrderBy(x => EF.Functions.Random()).Take(5).ToList(),
            })
            .OrderByDescending(x => x.TotalChaptersRead)
            .Take(5)
            .ToListAsync();

        var final = new List<MostReadAuthorsDto>();

        foreach (var m in res)
        {
            var randomChapters = await context.Chapter
                .Where(c => m.ChapterIds.Contains(c.Id))
                .Select(c => new
                {
                    Chapter = c,
                    SeriesId = c.Volume.Series.Id,
                    LibraryId = c.Volume.Series.LibraryId,
                })
                .ToListAsync();


            final.Add(new MostReadAuthorsDto
            {
                AuthorId = m.PersonId,
                AuthorName = m.AuthorName,
                TotalChaptersRead = m.TotalChaptersRead,
                Chapters = randomChapters.Select(x => new AuthorChapterDto
                {
                    LibraryId = x.LibraryId,
                    SeriesId = x.SeriesId,
                    ChapterId = x.Chapter.Id,
                    Title = x.Chapter.TitleName, // TODO: Use that method that makes a smart title? Do we have that? Where it falls back to Chapter #3 or whatever
                }).ToList(),
            });
        }

        return final;

    }

    public async Task<int> GetTotalReads(int userId, int requestingUserId)
    {
        var socialPreferences = await unitOfWork.UserRepository.GetSocialPreferencesForUser(userId);
        var requestingUser = await unitOfWork.UserRepository.GetUserByIdAsync(requestingUserId);

        var librariesForUser = await unitOfWork.LibraryRepository.GetLibraryIdsForUserIdAsync(userId);
        var filter = new StatsFilterDto
        {
            Libraries = librariesForUser,
        };

        return await context.AppUserReadingSessionActivityData
            .ApplyStatsFilter(filter, userId, socialPreferences, requestingUser, isAggregate: true)
            .CountAsync();
    }


    public async Task<IEnumerable<TopReadDto>> GetTopUsers(int days)
    {
        var libraries = (await unitOfWork.LibraryRepository.GetLibrariesAsync()).ToList();
        var users = (await unitOfWork.UserRepository.GetAllUsersAsync()).ToList();
        var minDate = DateTime.Now.Subtract(TimeSpan.FromDays(days));

        var topUsersAndReadChapters = context.AppUserProgresses
            .AsSplitQuery()
            .AsEnumerable()
            .GroupBy(sm => sm.AppUserId)
            .Select(sm => new
            {
                User = context.AppUser.Single(u => u.Id == sm.Key),
                Chapters = context.Chapter.Where(c => context.AppUserProgresses
                    .Where(u => u.AppUserId == sm.Key)
                    .Where(p => p.PagesRead > 0)
                    .Where(p => days == 0 || (p.Created >= minDate && p.LastModified >= minDate))
                    .Select(p => p.ChapterId)
                    .Distinct()
                    .Contains(c.Id))
            })
            .OrderByDescending(d => d.Chapters.Sum(c => c.AvgHoursToRead))
            .ToList();


        // Need a mapping of Library to chapter ids
        var chapterIdWithLibraryId = topUsersAndReadChapters
            .SelectMany(u => u.Chapters
                .Select(c => c.Id)).Select(d => new
                    {
                        LibraryId = context.Chapter.Where(c => c.Id == d).AsSplitQuery().Select(c => c.Volume).Select(v => v.Series).Select(s => s.LibraryId).Single(),
                        ChapterId = d
                    })
            .ToList();

        var chapterLibLookup = new Dictionary<int, int>();
        foreach (var cl in chapterIdWithLibraryId.Where(cl => !chapterLibLookup.ContainsKey(cl.ChapterId)))
        {
            chapterLibLookup.Add(cl.ChapterId, cl.LibraryId);
        }

        var user = new Dictionary<int, Dictionary<LibraryType, float>>();
        foreach (var userChapter in topUsersAndReadChapters)
        {
            if (!user.ContainsKey(userChapter.User.Id)) user.Add(userChapter.User.Id, []);
            var libraryTimes = user[userChapter.User.Id];

            foreach (var chapter in userChapter.Chapters)
            {
                var library = libraries.First(l => l.Id == chapterLibLookup[chapter.Id]);
                libraryTimes.TryAdd(library.Type, 0f);

                var existingHours = libraryTimes[library.Type];
                libraryTimes[library.Type] = existingHours + chapter.AvgHoursToRead;
            }

            user[userChapter.User.Id] = libraryTimes;
        }


        return user.Keys.Select(userId => new TopReadDto()
            {
                UserId = userId,
                Username = users.First(u => u.Id == userId).UserName,
                BooksTime = user[userId].TryGetValue(LibraryType.Book, out var bookTime) ? bookTime : 0 +
                    (user[userId].TryGetValue(LibraryType.LightNovel, out var bookTime2) ? bookTime2 : 0),
                ComicsTime = user[userId].TryGetValue(LibraryType.Comic, out var comicTime) ? comicTime : 0,
                MangaTime = user[userId].TryGetValue(LibraryType.Manga, out var mangaTime) ? mangaTime : 0,
            })
            .ToList();
    }
}
