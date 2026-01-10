using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Progress;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Progress;
using API.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace API.Services.Reading;

#nullable enable

public interface IReadingSessionService
{
    Task UpdateProgress(int userId, ProgressDto progressDto, ClientInfoData? clientInfo, int? deviceId);
}

public sealed class ReadingSessionService : IReadingSessionService, IDisposable, IAsyncDisposable
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ReadingSessionService> _logger;
    private readonly HybridCache _cache;
    private readonly TimeSpan _sessionTimeout;
    private readonly TimeSpan _pollInterval;
    private readonly Timer _cleanupTimer;
    private readonly SemaphoreSlim _cleanupLock = new(1, 1);
    private bool _disposed;

    private static readonly HybridCacheEntryOptions ChapterFormatCacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(30),
        LocalCacheExpiration = TimeSpan.FromMinutes(30)
    };

    public ReadingSessionService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ReadingSessionService> logger,
        HybridCache cache,
        TimeSpan? sessionTimeout = null,
        TimeSpan? pollInterval = null)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _cache = cache;
        _sessionTimeout = sessionTimeout ?? TimeSpan.FromMinutes(10);
        _pollInterval = pollInterval ?? TimeSpan.FromMinutes(5);

        _cleanupTimer = new Timer(
            callback: _ => _ = RunCleanupAsync(),
            state: null,
            dueTime: _pollInterval,
            period: _pollInterval
        );
    }

    public async Task UpdateProgress(int userId, ProgressDto progressDto, ClientInfoData? clientInfo, int? deviceId)
    {
        _logger.LogDebug("Updating Reading Session for {UserId} on {ChapterId}", userId, progressDto.ChapterId);

        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();

        var session = await GetOrCreateSessionAsync(userId, progressDto, context);

        await UpdateActivityDataAsync(session, progressDto, clientInfo, deviceId, scope, context);

        session.LastModified = DateTime.Now;
        session.LastModifiedUtc = DateTime.UtcNow;

        await context.SaveChangesAsync();
    }

    private async Task<AppUserReadingSession> GetOrCreateSessionAsync(int userId, ProgressDto dto, DataContext context)
    {
        var cutoffUtc = DateTime.UtcNow - _sessionTimeout;
        var midnightToday = DateTime.Today;

        var existingSession = await context.AppUserReadingSession
            .Where(s => s.IsActive && s.AppUserId == userId)
            .Where(s => s.LastModifiedUtc >= cutoffUtc && s.StartTime >= midnightToday)
            .Include(s => s.ActivityData)
            .FirstOrDefaultAsync();

        if (existingSession != null)
        {
            return existingSession;
        }

        var chapterFormat = await GetChapterFormatAsync(dto.ChapterId, context);
        var newSession = new AppUserReadingSession
        {
            AppUserId = userId,
            StartTime = DateTime.Now,
            StartTimeUtc = DateTime.UtcNow,
            LastModified = DateTime.Now,
            LastModifiedUtc = DateTime.UtcNow,
            IsActive = true,
            ActivityData = [NewActivityData(dto, chapterFormat)]
        };

        context.AppUserReadingSession.Add(newSession);
        await context.SaveChangesAsync();

        return newSession;
    }

    private async Task UpdateActivityDataAsync( AppUserReadingSession session, ProgressDto progressDto, ClientInfoData? clientInfo,
        int? deviceId, IServiceScope scope, DataContext context)
    {
        var existingActivity = session.ActivityData
            .FirstOrDefault(d => d.ChapterId == progressDto.ChapterId);

        var chapterFormat = await GetChapterFormatAsync(progressDto.ChapterId, context);

        if (existingActivity != null)
        {
            await UpdateExistingActivityAsync(
                existingActivity, progressDto, clientInfo, deviceId, chapterFormat, scope);
        }
        else
        {
            var newActivity = NewActivityData(progressDto, chapterFormat);
            if (clientInfo != null)
            {
                newActivity.ClientInfo = clientInfo;
            }
            if (deviceId.HasValue)
            {
                newActivity.DeviceIds.Add(deviceId.Value);
            }
            session.ActivityData.Add(newActivity);
        }
    }

    private async Task UpdateExistingActivityAsync( AppUserReadingSessionActivityData activity, ProgressDto progressDto, ClientInfoData? clientInfo,
        int? deviceId, MangaFormat chapterFormat, IServiceScope scope)
    {
        activity.PagesRead = progressDto.PageNum - activity.StartPage;
        activity.EndPage = progressDto.PageNum;
        activity.EndTime = DateTime.Now;
        activity.EndTimeUtc = DateTime.UtcNow;

        if (deviceId.HasValue && !activity.DeviceIds.Contains(deviceId.Value))
        {
            activity.DeviceIds.Add(deviceId.Value);
        }

        if (clientInfo != null)
        {
            activity.ClientInfo = clientInfo;
        }

        var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();
        var chapter = await cacheService.Ensure(progressDto.ChapterId);

        activity.TotalPages = chapter?.Pages ?? 0;
        activity.TotalWords = chapter?.WordCount ?? 0;

        if (chapterFormat == MangaFormat.Epub && chapter != null && !string.IsNullOrEmpty(progressDto.BookScrollId))
        {
            await UpdateEpubActivityAsync(activity, progressDto, chapter, cacheService, scope);
        }
    }

    private async Task UpdateEpubActivityAsync( AppUserReadingSessionActivityData activity, ProgressDto progressDto, Chapter chapter,
        ICacheService cacheService, IServiceScope scope)
    {
        var bookService = scope.ServiceProvider.GetRequiredService<IBookService>();
        var cachedFilePath = cacheService.GetCachedFile(chapter);

        if (string.IsNullOrEmpty(activity.StartBookScrollId))
        {
            activity.StartBookScrollId = progressDto.BookScrollId;
            activity.WordsRead = 0;
        }
        else
        {
            try
            {
                activity.WordsRead = await bookService.GetWordCountBetweenXPaths(
                    cachedFilePath,
                    activity.StartBookScrollId,
                    activity.StartPage,
                    progressDto.BookScrollId!,
                    progressDto.PageNum
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error calculating words read for activity on chapter {ChapterId}",
                    activity.ChapterId);
            }
        }

        activity.EndBookScrollId = progressDto.BookScrollId;
    }

    private async Task RunCleanupAsync()
    {
        if (!await _cleanupLock.WaitAsync(TimeSpan.Zero))
        {
            _logger.LogDebug("Cleanup already in progress, skipping");
            return;
        }

        try
        {
            await CleanupExpiredSessionsAsync();
        }
        finally
        {
            _cleanupLock.Release();
        }
    }

    private async Task CleanupExpiredSessionsAsync()
    {
        try
        {
            var cutoffUtc = DateTime.UtcNow - _sessionTimeout;
            var midnightToday = DateTime.Today;

            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DataContext>();
            var eventHub = scope.ServiceProvider.GetRequiredService<IEventHub>();

            var expiredSessions = await context.AppUserReadingSession
                .Where(s => s.IsActive)
                .Where(s => s.LastModifiedUtc < cutoffUtc || s.StartTime < midnightToday)
                .Include(s => s.ActivityData)
                .ToListAsync();

            if (expiredSessions.Count == 0) return;

            _logger.LogInformation("Closing {Count} expired reading sessions", expiredSessions.Count);

            var allCompletedChapterIds = new List<int>();

            foreach (var session in expiredSessions)
            {
                var completedIds = await CloseSessionAsync(session, eventHub);
                allCompletedChapterIds.AddRange(completedIds);
            }

            await context.SaveChangesAsync();

            // Batch update total reads
            if (allCompletedChapterIds.Count > 0)
            {
                var distinctChapterIds = allCompletedChapterIds.Distinct().ToList();
                await context.AppUserProgresses
                    .Where(p => distinctChapterIds.Contains(p.ChapterId))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.TotalReads, x => x.TotalReads + 1));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during session cleanup");
        }
    }

    private async Task<List<int>> CloseSessionAsync(
        AppUserReadingSession session,
        IEventHub eventHub)
    {
        var lastActivity = session.ActivityData
            .Where(ad => ad.EndTime.HasValue)
            .MaxBy(ad => ad.EndTime);

        var endTime = lastActivity?.EndTime ?? session.LastModified;
        var endTimeUtc = lastActivity?.EndTimeUtc ?? session.LastModifiedUtc;

        // Handle midnight rollover
        if (session.StartTime.Date < DateTime.Today)
        {
            var endOfStartDay = session.StartTime.Date.AddDays(1).AddTicks(-1);
            endTime = endOfStartDay;
            endTimeUtc = TimeZoneInfo.ConvertTimeToUtc(endOfStartDay);
        }

        session.IsActive = false;
        session.EndTime = endTime;
        session.EndTimeUtc = endTimeUtc;
        session.LastModified = DateTime.Now;
        session.LastModifiedUtc = DateTime.UtcNow;

        // Collect completed chapters
        var completedChapterIds = session.ActivityData
            .Where(d => d.TotalPages > 0 && d.EndPage >= d.TotalPages)
            .Select(d => d.ChapterId)
            .ToList();

        // Clear format caches
        foreach (var activity in session.ActivityData)
        {
            await _cache.RemoveAsync(GetChapterFormatCacheKey(activity.ChapterId));
        }

        // Notify clients
        await eventHub.SendMessageAsync(
            MessageFactory.SessionClose,
            MessageFactory.SessionCloseEvent(session.Id));

        _logger.LogDebug(
            "Closed session {SessionId} for user {UserId}, {ActivityCount} activities, {CompletedCount} completed chapters",
            session.Id, session.AppUserId, session.ActivityData.Count, completedChapterIds.Count);

        return completedChapterIds;
    }

    private async Task<MangaFormat> GetChapterFormatAsync(int chapterId, DataContext context)
    {
        var cacheKey = GetChapterFormatCacheKey(chapterId);

        return await _cache.GetOrCreateAsync(
            cacheKey,
            (chapterId, context),
            static async (state, cancel) =>
                await state.context.MangaFile
                    .Where(f => f.ChapterId == state.chapterId)
                    .Select(f => f.Format)
                    .FirstOrDefaultAsync(cancel),
            ChapterFormatCacheOptions);
    }

    private static string GetChapterFormatCacheKey(int chapterId)
        => $"readingsession_chapter_format_{chapterId}";

    private static AppUserReadingSessionActivityData NewActivityData(ProgressDto dto, MangaFormat format)
    {
        var startPage = format == MangaFormat.Epub ? dto.PageNum : Math.Max(dto.PageNum - 1, 0);

        return new AppUserReadingSessionActivityData
        {
            ChapterId = dto.ChapterId,
            VolumeId = dto.VolumeId,
            SeriesId = dto.SeriesId,
            LibraryId = dto.LibraryId,
            StartPage = startPage,
            EndPage = dto.PageNum,
            StartTime = DateTime.Now,
            StartTimeUtc = DateTime.UtcNow,
            EndTime = null,
            EndTimeUtc = null,
            PagesRead = 0,
            WordsRead = 0,
            ClientInfo = null,
            DeviceIds = [],
            Format = format,
        };
    }

    public void Dispose()
    {
        if (_disposed) return;

        _cleanupTimer.Dispose();
        _cleanupLock.Dispose();
        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        await _cleanupTimer.DisposeAsync();
        _cleanupLock.Dispose();
        _disposed = true;
    }
}
