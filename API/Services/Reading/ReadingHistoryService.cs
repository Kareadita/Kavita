using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Progress;
using API.Entities.Progress;
using Hangfire;
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

    public ReadingHistoryService(DataContext context, ILogger<ReadingHistoryService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task AggregateYesterdaysActivity()
    {
        var yesterday = DateTime.Today.AddDays(-1);
        var yesterdayUtc = DateTime.UtcNow.Date.AddDays(-1);

        // Define precise boundaries for yesterday
        var yesterdayStart = yesterday; // 2025-10-22 00:00:00.000
        var yesterdayEnd = yesterday.AddDays(1).AddTicks(-1); // 2025-10-22 23:59:59.9999999

        // First - Validate that all sessions are closed, if not, reschedule ourselves for 10 mins in future
        if (await _context.AppUserReadingSession.AnyAsync(s => s.IsActive || s.EndTime == null))
        {
            _logger.LogWarning("Not all reading sessions are closed, rescheduling for 10 minutes");
            BackgroundJob.Schedule(() => AggregateYesterdaysActivity(), TimeSpan.FromMinutes(10));
        }

        // Second - Validate we haven't already created a ReadingHistory for yesterday
        var existingHistoryUserIds = await _context.AppUserReadingHistory
            .Where(h => h.DateUtc == yesterdayUtc)
            .Select(h => h.AppUserId)
            .ToListAsync();

        if (existingHistoryUserIds.Count != 0)
        {
            _logger.LogInformation("Reading history already exists for {Count} users on {Date}",
                existingHistoryUserIds.Count, yesterday);
            return;
        }

        // Third - Get all closed sessions from yesterday using precise boundaries
        var yesterdaySessions = await _context.AppUserReadingSession
            .Where(s => !s.IsActive && s.EndTime.HasValue)
            .Where(s => s.StartTime >= yesterdayStart && s.StartTime <= yesterdayEnd)
            .Include(s => s.ActivityData)
            .ToListAsync();

        if (yesterdaySessions.Count == 0)
        {
            _logger.LogInformation("No reading sessions found for {Date}", yesterday);
            return;
        }

        // Fourth - Group by user and aggregate
        var userGroups = yesterdaySessions.GroupBy(s => s.AppUserId);
        var userCount = 0;

        foreach (var userGroup in userGroups)
        {
            var userId = userGroup.Key;
            var sessions = userGroup.ToList();

            // Calculate aggregates
            var totalMinutes = 0;
            var totalPages = 0;
            var totalWords = 0;
            var longestSessionMinutes = 0;
            var seriesIds = new List<int>();
            var chapterIds = new List<int>();

            var devicesUsed = sessions
                .SelectMany(s => s.ActivityData)
                .Select(a => a.ClientInfo)
                .Where(c => c != null)
                .DistinctBy(c => new { c.UserAgent, c.IpAddress, c.ClientType, c.Platform, c.DeviceType })
                .ToList();

            foreach (var session in sessions)
            {
                if (session.EndTime.HasValue)
                {
                    var sessionMinutes = (int)(session.EndTime.Value - session.StartTime).TotalMinutes;
                    totalMinutes += sessionMinutes;
                    longestSessionMinutes = Math.Max(longestSessionMinutes, sessionMinutes);
                }

                // Parse ActivityData JSON
                foreach (var activity in session.ActivityData)
                {
                    totalPages += activity.PagesRead;
                    totalWords += activity.WordsRead;
                    seriesIds.Add(activity.SeriesId);
                    chapterIds.Add(activity.ChapterId);
                }
            }

            var dailyData = new DailyReadingDataDto
            {
                TotalMinutesRead = totalMinutes,
                TotalPagesRead = totalPages,
                TotalWordsRead = totalWords,
                LongestSessionMinutes = longestSessionMinutes,
                SeriesIds = seriesIds.Distinct().ToList(),
                ChapterIds = chapterIds.Distinct().ToList()
            };

            // Create ReadingHistory record
            var history = new AppUserReadingHistory
            {
                AppUserId = userId,
                DateUtc = yesterdayUtc,
                Data = dailyData,
                CreatedUtc = DateTime.UtcNow,
                ClientInfoUsed = devicesUsed
            };

            _context.AppUserReadingHistory.Add(history);
            userCount++;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Aggregated reading history for {UserCount} users on {Date}",
            userCount, yesterday);

    }
}
