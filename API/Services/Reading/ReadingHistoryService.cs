using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Progress;
using API.Entities.Enums;
using API.Entities.Progress;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Services.Reading;
#nullable enable

public interface IReadingHistoryService
{
    Task AggregateYesterdaysActivity();
}

public class ReadingHistoryService : IReadingHistoryService
{
    private readonly DataContext _context;
    private readonly ILogger<ReadingHistoryService> _logger;

    private sealed record ChapterMetadata(int Id, string? Range, float VolumeNumber, string SeriesName, string? LocalizedSeriesName, string LibraryName, LibraryType LibraryType);
    private sealed record SeriesMetadata(int Id, string Name, string? LocalizedName, string LibraryName, LibraryType LibraryType);

    public ReadingHistoryService(DataContext context, ILogger<ReadingHistoryService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task AggregateYesterdaysActivity()
    {
        var yesterdayUtc = DateTime.UtcNow.Date.AddDays(-1);
        var startUtc = yesterdayUtc;
        var endUtc = yesterdayUtc.AddDays(1).AddTicks(-1);

        var usersToProcess = await GetUsersPendingAggregation(startUtc, endUtc, yesterdayUtc);

        foreach (var userId in usersToProcess)
        {
            await AggregateUserActivity(userId, startUtc, endUtc, yesterdayUtc);
        }

        await _context.SaveChangesAsync();
    }

    private async Task<List<int>> GetUsersPendingAggregation(DateTime start, DateTime end, DateTime reportDate)
    {
        var needAggregationUserIds = await _context.AppUserReadingSession
            .Where(s => s.StartTime >= start && s.StartTime <= end)
            .Where(s => !s.IsActive && s.EndTime != null)
            .Select(s => s.AppUserId)
            .Distinct()
            .ToListAsync();

        var alreadyHasHistoryUserIds = await _context.AppUserReadingHistory
            .Where(h => h.DateUtc == reportDate)
            .Select(h => h.AppUserId)
            .ToListAsync();

        return needAggregationUserIds.Except(alreadyHasHistoryUserIds).ToList();
    }

    private async Task AggregateUserActivity(int userId, DateTime start, DateTime end, DateTime reportDate)
    {
        var sessions = await _context.AppUserReadingSession
            .Include(s => s.ActivityData)
            .Where(s => s.AppUserId == userId &&
                        s.StartTime >= start && s.StartTime <= end &&
                        !s.IsActive && s.EndTime != null)
            .ToListAsync();

        if (sessions.Count == 0) return;

        var chapterMeta = await GetChapterMetadata(sessions);
        var seriesMeta = await GetSeriesMetadata(sessions);

        var dailyData = CalculateDailyData(sessions, chapterMeta, seriesMeta);

        _context.AppUserReadingHistory.Add(new AppUserReadingHistory
        {
            AppUserId = userId,
            DateUtc = reportDate,
            ClientInfoUsed = ExtractClientInfo(sessions),
            Data = dailyData
        });
    }

    private async Task<Dictionary<int, ChapterMetadata>> GetChapterMetadata(List<AppUserReadingSession> sessions)
    {
        var ids = sessions.SelectMany(s => s.ActivityData.Select(ad => ad.ChapterId)).Distinct().ToList();
        return await _context.Chapter
            .Where(c => ids.Contains(c.Id))
            .Select(c => new ChapterMetadata(
                c.Id, c.Range, c.Volume.MinNumber, c.Volume.Series.Name,
                c.Volume.Series.LocalizedName, c.Volume.Series.Library.Name,
                c.Volume.Series.Library.Type))
            .ToDictionaryAsync(c => c.Id);
    }

    private async Task<Dictionary<int, SeriesMetadata>> GetSeriesMetadata(List<AppUserReadingSession> sessions)
    {
        var ids = sessions.SelectMany(s => s.ActivityData.Select(ad => ad.SeriesId)).Distinct().ToList();
        return await _context.Series
            .Where(s => ids.Contains(s.Id))
            .Select(s => new SeriesMetadata(s.Id, s.Name, s.LocalizedName, s.Library.Name, s.Library.Type))
            .ToDictionaryAsync(s => s.Id);
    }

    private static DailyReadingDataDto CalculateDailyData(List<AppUserReadingSession> sessions,
        Dictionary<int, ChapterMetadata> chapterMeta, Dictionary<int, SeriesMetadata> seriesMeta)
    {
        var totalMinutes = 0;
        var totalPages = 0;
        var totalWords = 0;
        var longestSession = 0;
        var seriesIds = new HashSet<int>();
        var chapterIds = new HashSet<int>();
        var activities = new List<ReadingActivitySnapshotDto>();

        foreach (var session in sessions)
        {
            var duration = (int)(session.EndTime!.Value - session.StartTime).TotalMinutes;
            totalMinutes += duration;
            longestSession = Math.Max(longestSession, duration);

            foreach (var activity in session.ActivityData)
            {
                totalPages += activity.PagesRead;
                totalWords += activity.WordsRead;
                chapterIds.Add(activity.ChapterId);
                seriesIds.Add(activity.SeriesId);

                activities.Add(MapToSnapshot(activity, chapterMeta, seriesMeta));
            }
        }

        return new DailyReadingDataDto
        {
            TotalMinutesRead = totalMinutes,
            TotalPagesRead = totalPages,
            TotalWordsRead = totalWords,
            LongestSessionMinutes = longestSession,
            SeriesIds = seriesIds.Cast<int?>().ToList(),
            ChapterIds = chapterIds.Cast<int?>().ToList(),
            Activities = activities
        };
    }

    private static ReadingActivitySnapshotDto MapToSnapshot( AppUserReadingSessionActivityData activity,
        Dictionary<int, ChapterMetadata> chapterLookup, Dictionary<int, SeriesMetadata> seriesLookup)
    {
        var minutesRead = activity.EndTimeUtc.HasValue
            ? (int)(activity.EndTimeUtc.Value - activity.StartTimeUtc).TotalMinutes
            : 0;

        var snapshot = new ReadingActivitySnapshotDto
        {
            ChapterId = activity.ChapterId,
            VolumeId = activity.VolumeId,
            SeriesId = activity.SeriesId,
            LibraryId = activity.LibraryId,
            Format = activity.Format,
            PagesRead = activity.PagesRead,
            WordsRead = activity.WordsRead,
            MinutesRead = minutesRead,
            StartTimeUtc = activity.StartTimeUtc,
            EndTimeUtc = activity.EndTimeUtc ?? activity.StartTimeUtc,

            // Set defaults for required strings
            SeriesName = string.Empty,
            LibraryName = string.Empty,
            ChapterRange = string.Empty
        };

        if (chapterLookup.TryGetValue(activity.ChapterId, out var c))
        {
            snapshot.SeriesName = c.SeriesName;
            snapshot.LocalizedSeriesName = c.LocalizedSeriesName;
            snapshot.ChapterRange = c.Range ?? string.Empty;
            snapshot.VolumeNumber = c.VolumeNumber;
            snapshot.LibraryName = c.LibraryName;
            snapshot.LibraryType = c.LibraryType;
        }
        else if (seriesLookup.TryGetValue(activity.SeriesId, out var s))
        {
            snapshot.SeriesName = s.Name;
            snapshot.LocalizedSeriesName = s.LocalizedName;
            snapshot.LibraryName = s.LibraryName;
            snapshot.LibraryType = s.LibraryType;
            snapshot.ChapterRange = "[Deleted]";
        }
        else
        {
            snapshot.SeriesName = "[Deleted Data]";
            snapshot.ChapterRange = "[Deleted]";
        }

        return snapshot;
    }

    private static List<ClientInfoData> ExtractClientInfo(List<AppUserReadingSession> sessions)
    {
        return sessions
            .SelectMany(s => s.ActivityData)
            .Select(a => a.ClientInfo)
            .Where(c => c != null)
            .Select(c => c!)
            .DistinctBy(c => new { c.UserAgent, c.IpAddress, c.Platform })
            .ToList();
    }
}
