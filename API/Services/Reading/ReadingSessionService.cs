using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Progress;
using API.Entities.Enums;
using API.Entities.Progress;
using API.Extensions;
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

internal sealed record SessionTimeout<T>
{
    public required T Value { get; set; }
    /// <summary>
    /// Expiration time in Utc
    /// </summary>
    public DateTime ExpirationUtc { get; set; }
    public DateTime LastTimerRefresh { get; set; }
    public Timer? TimeoutTimer { get; set; }
}

public sealed class ReadingSessionService : IReadingSessionService, IDisposable, IAsyncDisposable
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ReadingSessionService> _logger;
    private readonly HybridCache _cache;
    private readonly ConcurrentDictionary<string, SessionTimeout<int>> _activeSessions = new();
    private readonly int _defaultTimeoutMinutes;
    private readonly int _timerRefreshDebounceSeconds;
    private Timer? _midnightRolloverTimer;
    private bool _disposed;

    private static readonly HybridCacheEntryOptions ChapterFormatCacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(30),
        LocalCacheExpiration = TimeSpan.FromMinutes(30)
    };

    public ReadingSessionService(IServiceScopeFactory serviceScopeFactory, ILogger<ReadingSessionService> logger, HybridCache cache,
        int defaultTimeoutMinutes = 30, int timerRefreshDebounceSeconds = 5)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _cache = cache;

        _defaultTimeoutMinutes = defaultTimeoutMinutes;
        _timerRefreshDebounceSeconds = timerRefreshDebounceSeconds;

        ScheduleMidnightRollover();
    }


    public async Task UpdateProgress(int userId, ProgressDto progressDto, ClientInfoData? clientInfo, int? deviceId)
    {
        _logger.LogDebug("Creating/Updating Reading Session for {UserId} on {ChapterId}", userId, progressDto.ChapterId);

        var session = await GetOrCreateSession(userId, progressDto);

        using var scope = _serviceScopeFactory.CreateScope();

        // Update session activity data in DB
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();

        // If Chapter doesn't exist already, add
        var existingChapterActivity = session.ActivityData.FirstOrDefault(d => d.ChapterId == progressDto.ChapterId);

        // Use cached chapter format to avoid repeated DB queries
        var chapterFormat = await GetChapterFormatAsync(progressDto.ChapterId, context);

        if (existingChapterActivity != null)
        {
            existingChapterActivity.PagesRead = progressDto.PageNum - existingChapterActivity.StartPage;
            existingChapterActivity.EndPage = progressDto.PageNum;
            existingChapterActivity.EndTime = DateTime.Now;
            existingChapterActivity.EndTimeUtc = DateTime.UtcNow;
            if (deviceId.HasValue)
            {
                existingChapterActivity.DeviceIds.Add(deviceId.Value);
            }

            existingChapterActivity.DeviceIds = existingChapterActivity.DeviceIds.Distinct().ToList();


            // Update client info if it changed (e.g., user switched devices)
            if (clientInfo != null)
            {
                existingChapterActivity.ClientInfo = clientInfo;
            }


            var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();
            var chapter = await cacheService.Ensure(progressDto.ChapterId);


            // Store total pages/words in case it changes in the future
            existingChapterActivity.TotalPages = chapter?.Pages ?? 0;
            existingChapterActivity.TotalWords = chapter?.WordCount ?? 0;


            if (chapterFormat == MangaFormat.Epub && !string.IsNullOrEmpty(progressDto.BookScrollId))
            {
                var bookService = scope.ServiceProvider.GetRequiredService<IBookService>();

                var cachedFilePath = cacheService.GetCachedFile(chapter!);

                // First update - capture starting position
                if (string.IsNullOrEmpty(existingChapterActivity.StartBookScrollId))
                {
                    existingChapterActivity.StartBookScrollId = progressDto.BookScrollId;
                    existingChapterActivity.WordsRead = 0;
                }
                else
                {
                    // Calculate total words read from start to current position
                    try
                    {
                        existingChapterActivity.WordsRead = await bookService.GetWordCountBetweenXPaths(
                            cachedFilePath,
                            existingChapterActivity.StartBookScrollId,
                            existingChapterActivity.StartPage,
                            progressDto.BookScrollId,
                            progressDto.PageNum
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "There was an error calculating words read for reading session {SessionId} on book {File}", session.Id, cachedFilePath);
                    }
                }

                // Always update the current end position
                existingChapterActivity.EndBookScrollId = progressDto.BookScrollId;
            }
        }
        else
        {
            // Add new ActivityData for a different chapter in the same session
            var newActivity = NewActivityData(progressDto, chapterFormat);
            if (clientInfo != null)
            {
                newActivity.ClientInfo = clientInfo;
                newActivity.DeviceIds.Add(deviceId!.Value);

                newActivity.DeviceIds = newActivity.DeviceIds.Distinct().ToList();
            }
            session.ActivityData.Add(newActivity);
        }

        // Update session timestamps
        session.LastModified = DateTime.Now;
        session.LastModifiedUtc = DateTime.UtcNow;

        // Save changes
        context.AppUserReadingSession.Update(session);
        await context.SaveChangesAsync();


        // Refresh timeout
        var cacheKey = GenerateCacheKey(userId, progressDto.ChapterId);
        RefreshSessionTimeout(cacheKey, session.Id);

    }

    private async Task<MangaFormat> GetChapterFormatAsync(int chapterId, DataContext context)
    {
        var cacheKey = GetChapterFormatCacheKey(chapterId);

        return await _cache.GetOrCreateAsync(
            cacheKey,
            (chapterId, context),
            async (state, cancel) =>
                await state.context.MangaFile
                    .Where(f => f.ChapterId == state.chapterId)
                    .Select(f => f.Format)
                    .FirstOrDefaultAsync(cancel),
            ChapterFormatCacheOptions);
    }

    private async Task ClearChapterFormatCache(int chapterId)
    {
        var cacheKey = GetChapterFormatCacheKey(chapterId);
        await _cache.RemoveAsync(cacheKey);
    }

    private static string GetChapterFormatCacheKey(int chapterId)
    {
        return $"readingsession_chapter_format_{chapterId}";
    }

    private async Task ClearSessionChapterCaches(int sessionId)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DataContext>();

            var chapterIds = await context.AppUserReadingSession
                .Where(s => s.Id == sessionId)
                .SelectMany(s => s.ActivityData)
                .Select(ad => ad.ChapterId)
                .Distinct()
                .ToListAsync();

            foreach (var chapterId in chapterIds)
            {
                await ClearChapterFormatCache(chapterId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear chapter format caches for session {SessionId}", sessionId);
        }
    }

    private async Task<AppUserReadingSession> GetOrCreateSession(int userId, ProgressDto dto)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();

        // Check if we have an existing cached reading session that is active
        var cacheKey = GenerateCacheKey(userId, dto.ChapterId);
        if (_activeSessions.TryGetValue(cacheKey, out var sessionTimeout))
        {
            if (sessionTimeout.ExpirationUtc <= DateTime.UtcNow)
            {
                // Expired - close it and create new one
                await CloseSession(cacheKey, sessionTimeout.Value);
            }
            else
            {
                var session = await context.AppUserReadingSession
                    .Where(s => s.Id == sessionTimeout.Value)
                    .Include(s => s.ActivityData)
                    .FirstOrDefaultAsync();

                if (session != null) return session;
            }
        }

        // Look up in the DB for an active reading session
        var dbSession = await context.AppUserReadingSession
            .Where(s => s.IsActive && s.AppUserId == userId)
            .Include(s => s.ActivityData)
            .FirstOrDefaultAsync();

        if (dbSession != null)
        {
            // Re-add to cache with timer
            RefreshSessionTimeout(cacheKey, dbSession.Id);
            return dbSession;
        }

        var chapterFormat = await GetChapterFormatAsync(dto.ChapterId, context);

        // Create a new session and return it
        var newSession = new AppUserReadingSession()
            {
                AppUserId = userId,
                StartTime = DateTime.Now,
                StartTimeUtc = DateTime.UtcNow,
                IsActive = true,
                ActivityData =
                [
                    NewActivityData(dto, chapterFormat),
                ]
            };

        await context.AppUserReadingSession.AddAsync(newSession);
        await context.SaveChangesAsync();

        RefreshSessionTimeout(cacheKey, newSession.Id);

        return newSession;
    }

    private static AppUserReadingSessionActivityData NewActivityData(ProgressDto dto, MangaFormat format)
    {
        var page = format == MangaFormat.Epub ? dto.PageNum : Math.Max(dto.PageNum - 1, 0);
        return new AppUserReadingSessionActivityData
        {
            ChapterId = dto.ChapterId,
            VolumeId = dto.VolumeId,
            SeriesId = dto.SeriesId,
            LibraryId = dto.LibraryId,
            StartPage = page,
            EndPage = dto.PageNum,
            StartTime = DateTime.Now,
            StartTimeUtc = DateTime.UtcNow,
            EndTime = null,
            PagesRead = 0,
            WordsRead = 0,
            ClientInfo = null,
            DeviceIds = [],
            Format = format,
        };
    }


    private void RefreshSessionTimeout(string cacheKey, int sessionId)
    {
        var now = DateTime.Now;

        _activeSessions.AddOrUpdate(cacheKey,
            // Add new
            key => new SessionTimeout<int>()
            {
                Value = sessionId,
                ExpirationUtc = now.AddMinutes(_defaultTimeoutMinutes),
                LastTimerRefresh = now,
                TimeoutTimer = CreateSessionTimer(key, sessionId)
            },
            // Update Existing
            (_, existing) =>
            {
                // Always update expiration
                existing.ExpirationUtc = now.AddMinutes(_defaultTimeoutMinutes);

                // Debounce timer refresh (avoid excessive timer churn)
                var secondsSinceLastRefresh = (now - existing.LastTimerRefresh).TotalSeconds;
                if (secondsSinceLastRefresh >= _timerRefreshDebounceSeconds)
                {
                    existing.TimeoutTimer?.Change(TimeSpan.FromMinutes(_defaultTimeoutMinutes), TimeSpan.Zero);

                    existing.LastTimerRefresh = now;
                }

                return existing;
            }
        );
    }

    private Timer CreateSessionTimer(string cacheKey, int sessionId)
    {
        return new Timer(
            callback: _ => OnSessionTimeout(cacheKey, sessionId),
            state: null,
            dueTime: TimeSpan.FromMinutes(_defaultTimeoutMinutes),
            period: TimeSpan.Zero
        );
    }

    private void OnSessionTimeout(string cacheKey, int sessionId)
    {
        _ = Task.Run(async () =>
            {
                await CloseSession(cacheKey, sessionId);
                await ClearSessionChapterCaches(sessionId);
            })
            .ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    _logger.LogError(t.Exception, "There was an issue closing session {SessionId} with CacheKey: {CacheKey}",
                        sessionId, cacheKey);
                }
            });
    }

    private async Task CloseSession(string cacheKey, int sessionId)
    {
        // Remove from cache and dispose timer
        if (_activeSessions.TryRemove(cacheKey, out var session) && session.TimeoutTimer != null)
        {
            await session.TimeoutTimer.DisposeAsync();
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();
        var eventHub = scope.ServiceProvider.GetRequiredService<IEventHub>();

        // Get the actual last activity end time from ActivityData
        var lastActivityTime = await context.AppUserReadingSessionActivityData
            .Where(ad => ad.AppUserReadingSessionId == sessionId && ad.EndTime.HasValue)
            .MaxAsync(ad => (DateTime?)ad.EndTime);

        var lastActivityTimeUtc = await context.AppUserReadingSessionActivityData
            .Where(ad => ad.AppUserReadingSessionId == sessionId && ad.EndTimeUtc.HasValue)
            .MaxAsync(ad => (DateTime?)ad.EndTimeUtc);

        if (lastActivityTime == null) return;

        // Use the session's LastModified as the EndTime (the actual last activity) and mark session as inactive
        await context.AppUserReadingSession
            .Where(s => s.Id == sessionId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsActive, false)
                .SetProperty(x => x.EndTime, lastActivityTime)
                .SetProperty(x => x.EndTimeUtc, lastActivityTimeUtc)
                .SetProperty(x => x.LastModified, DateTime.Now)
                .SetProperty(x => x.LastModifiedUtc, DateTime.UtcNow));

        await UpdateTotalReadsOnSessionClose(sessionId);

        // Trigger a SessionClose Event so Activity Feed can update
        await eventHub.SendMessageAsync(MessageFactory.SessionClose, MessageFactory.SessionCloseEvent(sessionId));
    }

    private async Task UpdateTotalReadsOnSessionClose(int sessionId)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();

        // Check if the user fully read any chapter and increment totalReads for said chapter
        var sessionEntry = await context.AppUserReadingSession
            .Where(s => s.Id == sessionId)
            .Include(s => s.ActivityData)
            .FirstAsync();

        var chapterIds = sessionEntry.ActivityData
            .Where(d => d.EndPage >= d.TotalPages)
            .Select(d => d.ChapterId)
            .ToList();

        if (chapterIds.Count > 0)
        {
            await context.AppUserProgresses
                .Where(p => chapterIds.Contains(p.ChapterId))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.TotalReads, x => x.TotalReads + 1));
        }
    }

    private void ScheduleMidnightRollover()
    {
        var now = DateTime.Now;
        var nextMidnight = now.Date.AddDays(1);
        var timeUntilMidnight = nextMidnight - now;

        _midnightRolloverTimer = new Timer(
            callback: _ =>
            {
                // Synchronous callback that starts async work
                OnMidnightRolloverAsync().ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        _logger.LogCritical("There was an issue closing midnight sessions");
                    }
                });
            },
            state: null,
            dueTime: timeUntilMidnight,
            period: TimeSpan.Zero
        );
    }

    private async Task OnMidnightRolloverAsync()
    {
        var endOfYesterday = DateTime.Now.Date.AddTicks(-1); // 23:59:59.9999999
        var endOfYesterdayUtc = TimeZoneInfo.ConvertTimeToUtc(endOfYesterday);
        var sessionsToClose = _activeSessions.ToArray();

        if (sessionsToClose.Length > 0)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DataContext>();
            var eventHub = scope.ServiceProvider.GetRequiredService<IEventHub>();

            var sessionIds = sessionsToClose.Select(kvp => kvp.Value.Value).ToList();

            // Batch close all sessions in DB
            await context.AppUserReadingSession
                .Where(s => sessionIds.Contains(s.Id))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.IsActive, false)
                    .SetProperty(x => x.EndTime, endOfYesterday)
                    .SetProperty(x => x.EndTimeUtc, endOfYesterdayUtc)
                    .SetProperty(x => x.LastModified, DateTime.Now)
                    .SetProperty(x => x.LastModifiedUtc, DateTime.UtcNow));

            // Ensure we increment total reads for any closed sessions
            var chapterIds = await context.AppUserReadingSession
                .Where(s => sessionIds.Contains(s.Id))
                .Include(s => s.ActivityData)
                .SelectMany(s => s.ActivityData
                    .Where(d => d.EndPage >= d.TotalPages)
                    .Select(d => d.ChapterId))
                .Distinct()
                .ToListAsync();

            if (chapterIds.Count > 0)
            {
                await context.AppUserProgresses
                    .Where(p => chapterIds.Contains(p.ChapterId))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.TotalReads, x => x.TotalReads + 1));
            }

            foreach (var sessionId in sessionIds)
            {
                await ClearSessionChapterCaches(sessionId);

                // Trigger a SessionClose Event so Activity Feed can update
                await eventHub.SendMessageAsync(MessageFactory.SessionClose, MessageFactory.SessionCloseEvent(sessionId));
            }

            // Clear cache and dispose all timers
            foreach (var kvp in sessionsToClose)
            {
                if (kvp.Value.TimeoutTimer != null) await kvp.Value.TimeoutTimer.DisposeAsync();
                _activeSessions.TryRemove(kvp.Key, out _);
            }
        }

        // Schedule next midnight Rollover
        ScheduleMidnightRollover();
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);

        Dispose(disposing: false);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            foreach (var session in _activeSessions.Values)
            {
                session.TimeoutTimer?.Dispose();
            }

            _midnightRolloverTimer?.Dispose();
            _activeSessions.Clear();
        }

        _disposed = true;
    }

    private async ValueTask DisposeAsyncCore()
    {
        if (_disposed) return;

        // Dispose managed resources asynchronously
        foreach (var session in _activeSessions.Values)
        {
            if (session.TimeoutTimer != null)
            {
                await session.TimeoutTimer.DisposeAsync().ConfigureAwait(false);
            }
        }

        if (_midnightRolloverTimer != null)
        {
            await _midnightRolloverTimer.DisposeAsync().ConfigureAwait(false);
        }

        _activeSessions.Clear();

        _disposed = true;
    }

    private static string GenerateCacheKey(int userId, int chapterId)
    {
        return $"{userId}_{chapterId}";
    }
}
