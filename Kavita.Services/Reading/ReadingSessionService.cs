using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.Reading;
using Kavita.API.Services.SignalR;
using Kavita.Common;
using Kavita.Database.Extensions;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Progress;
using Kavita.Models.DTOs.Reader;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Progress;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Reading;

public sealed class ReadingSessionService : IReadingSessionService, IDisposable, IAsyncDisposable
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ReadingSessionService> _logger;
    private readonly HybridCache _cache;
    private readonly TimeSpan _sessionTimeout;
    private readonly Timer _cleanupTimer;
    private readonly SemaphoreSlim _cleanupLock = new(1, 1);
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> UserLocks = new();
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
        var pollInterval1 = pollInterval ?? TimeSpan.FromMinutes(5);

        _cleanupTimer = new Timer(
            callback: _ => _ = RunCleanupAsync(),
            state: null,
            dueTime: pollInterval1,
            period: pollInterval1
        );
    }

    public async Task UpdateProgress(int userId, ProgressDto progressDto, ClientInfoData? clientInfo, int? deviceId)
    {
        // We need to lock per-user as progress events can come fast and duplicate, as we are using new DataContext per Background Task
        var userLock = UserLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));

        await userLock.WaitAsync();

        try
        {
            _logger.LogDebug("Updating Reading Session for {UserId} on {ChapterId}", userId, progressDto.ChapterId);

            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IDataContext>();
            var eventHub = scope.ServiceProvider.GetRequiredService<IEventHub>();

            var session = await GetOrCreateSessionAsync(userId, progressDto, context);

            await UpdateActivityDataAsync(session, progressDto, clientInfo, deviceId, scope, context);

            session.LastModified = DateTime.Now;
            session.LastModifiedUtc = DateTime.UtcNow;

            await context.SaveChangesAsync();

            await eventHub.SendMessageAsync(MessageFactory.ReadingSessionUpdate,
                MessageFactory.ReadingSessionUpdateEvent(userId, session.Id));
        }
        finally
        {
            userLock.Release();
        }
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task GenerateReadingSessionForChapters(int userId, int seriesId, Dictionary<int, int> chaptersMap, CancellationToken ct = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var readerService = scope.ServiceProvider.GetRequiredService<IReaderService>();
        var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();

        var series = await context.Series.FirstOrDefaultAsync(s => s.Id == seriesId, ct);
        if (series == null) throw new KavitaNotFoundException();

        var chapterIds = chaptersMap.Keys.ToList();

        _logger.LogDebug("Generating Reading Session for {UserId} on {SeriesId} for {ChapterCount} chapters",
            userId, seriesId, chapterIds.Count);

        var chapters = await context.Chapter
            .Where(cp => chapterIds.Contains(cp.Id) && cp.Volume.SeriesId == seriesId)
            .ApplyDefaultChapterOrdering()
            .ProjectToWithProgress<Chapter, ChapterDto>(mapper, userId)
            .ToListAsync(ct);

        // ApplyDefaultChapterOrdering orders from first to last, we want last to first
        chapters.Reverse();

        Dictionary<int, HourEstimateRangeDto> estimatedHoursByChapter = [];
        foreach (var chapterId in chapters.Select(cp => cp.Id))
        {
            var page = chaptersMap[chapterId];
            estimatedHoursByChapter[chapterId] = await readerService
                .GetEstimateFromPageForChapter(userId, seriesId, chapterId, page, ct);
        }

        var chapterSchedule = ScheduleChapters(estimatedHoursByChapter, DateTime.Now);

        AppUserReadingSession? currentSession = null;
        DateTime? currentSessionDate = null;
        List<AppUserReadingSession> addedSessions = [];

        foreach (var chapter in chapters)
        {
            var estimate = estimatedHoursByChapter[chapter.Id];
            if (estimate is { PageCount: 0, WordCount: 0 })
            {
                _logger.LogTrace("Not generating reading session for chapter {ChapterId} as nothing would be read", chapter.Id);
                continue;
            }

            var schedule = chapterSchedule[chapter.Id];
            var chapterDate = schedule.Start.Date;

            _logger.LogTrace("Chapter {ChapterId} will be read from {Start} to {End} [{Minutes}m]",
                chapter.Id, schedule.Start, schedule.End, schedule.End.Subtract(schedule.Start).TotalMinutes);

            if (currentSession == null || chapterDate != currentSessionDate)
            {
                currentSession = new AppUserReadingSession
                {
                    AppUserId = userId,
                    IsActive = false,
                    IsGenerated = true,
                    ActivityData = []
                };

                addedSessions.Add(currentSession);
                context.AppUserReadingSession.Add(currentSession);

                currentSessionDate = chapterDate;
            }

            currentSession.ActivityData.Add(new AppUserReadingSessionActivityData
            {
                LibraryId = series.LibraryId,
                SeriesId = seriesId,
                VolumeId = chapter.VolumeId,
                ChapterId = chapter.Id,
                StartPage = chapter.PagesRead,
                EndPage = chapter.Pages,
                StartTime = schedule.Start,
                StartTimeUtc = schedule.Start.ToUniversalTime(),
                EndTime = schedule.End,
                EndTimeUtc = schedule.End.ToUniversalTime(),
                PagesRead = estimate.PageCount,
                WordsRead = (int) estimate.WordCount,
                TotalPages = chapter.Pages,
                TotalWords = chapter.WordCount,
                Format = series.Format,
            });
        }

        foreach (var s in addedSessions)
        {
            s.StartTime = s.ActivityData.Min(ad => ad.StartTime);
            s.StartTimeUtc = s.ActivityData.Min(ad => ad.StartTimeUtc);
            s.EndTime = s.ActivityData.Max(ad => ad.EndTime);
            s.EndTimeUtc = s.ActivityData.Max(ad => ad.EndTimeUtc);
        }

        _logger.LogTrace("Generated {Count} reading sessions for {UserId} on {SeriesId} for {ChapterCount} chapters",
            addedSessions.Count, userId, seriesId, chapterIds.Count);

        await context.SaveChangesAsync(ct);
    }

    private static readonly TimeSpan BackfillClockSkew = TimeSpan.FromMinutes(5);

    public async Task<IList<BackfillReadingStatus>> BackfillReadingSessions(int userId, IList<BackfillReadingEntryDto> entries, CancellationToken ct = default)
    {
        var results = new BackfillReadingStatus[entries.Count];
        if (entries.Count == 0) return results;

        // Same lock as live progress, so a backfill cannot interleave with the session being built right now
        var userLock = UserLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        await userLock.WaitAsync(ct);

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IDataContext>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var nowUtc = DateTime.UtcNow;
            var oldestUtc = nowUtc - TimeSpan.FromDays(BackfillReadingResultDto.MaxAgeDays);

            var candidates = new List<BackfillCandidate>();
            var accessByChapter = new Dictionary<int, bool>();
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var start = AsUtc(entry.StartTimeUtc);
                var end = AsUtc(entry.EndTimeUtc);

                // A clock that runs a little ahead is not worth refusing; the reading just ends now
                if (end > nowUtc && end - nowUtc <= BackfillClockSkew) end = nowUtc;

                if (end < start || entry.StartPage < 0 || entry.EndPage < 0) results[i] = BackfillReadingStatus.Invalid;
                else if (end > nowUtc) results[i] = BackfillReadingStatus.InFuture;
                else if (start < oldestUtc) results[i] = BackfillReadingStatus.TooOld;
                else
                {
                    if (!accessByChapter.TryGetValue(entry.ChapterId, out var hasAccess))
                    {
                        hasAccess = await unitOfWork.UserRepository.HasAccessToChapter(userId, entry.ChapterId, ct);
                        accessByChapter[entry.ChapterId] = hasAccess;
                    }

                    if (!hasAccess) results[i] = BackfillReadingStatus.NoAccess;
                    else candidates.Add(new BackfillCandidate(i, entry, start, end));
                }
            }

            if (candidates.Count == 0) return results;

            var chapterIds = candidates.Select(c => c.Entry.ChapterId).Distinct().ToList();
            var chapters = await context.Chapter
                .Where(c => chapterIds.Contains(c.Id))
                .Select(c => new BackfillChapter(c.Id, c.VolumeId, c.Volume.SeriesId, c.Volume.Series.LibraryId, c.Pages, c.WordCount))
                .ToDictionaryAsync(c => c.Id, ct);

            // A client that never saw its request succeed will send it again. Reading that is already here is not added twice
            var recorded = (await context.AppUserReadingSessionActivityData
                    .Where(d => d.ReadingSession.AppUserId == userId && chapterIds.Contains(d.ChapterId) && d.StartTimeUtc >= oldestUtc)
                    .Select(d => new { d.ChapterId, d.StartTimeUtc, d.EndTimeUtc })
                    .ToListAsync(ct))
                .Select(d => (d.ChapterId, Start: Truncate(d.StartTimeUtc), End: d.EndTimeUtc.HasValue ? Truncate(d.EndTimeUtc.Value) : (DateTime?) null))
                .ToHashSet();

            var toRecord = new List<BackfillCandidate>();
            foreach (var candidate in candidates)
            {
                if (!chapters.ContainsKey(candidate.Entry.ChapterId))
                {
                    results[candidate.Index] = BackfillReadingStatus.NoAccess;
                    continue;
                }

                var key = (candidate.Entry.ChapterId, Truncate(candidate.Start), (DateTime?) Truncate(candidate.End));
                if (!recorded.Add(key))
                {
                    results[candidate.Index] = BackfillReadingStatus.AlreadyRecorded;
                    continue;
                }

                toRecord.Add(candidate);
            }

            if (toRecord.Count == 0) return results;

            var sessions = new List<AppUserReadingSession>();
            AppUserReadingSession? current = null;
            foreach (var candidate in toRecord.OrderBy(c => c.Start))
            {
                var chapter = chapters[candidate.Entry.ChapterId];
                var format = await GetChapterFormatAsync(chapter.Id, context);
                var activity = await NewBackfillActivityAsync(candidate, chapter, format, scope);

                // One session is one sitting, as when it is built from live progress: it ends after a pause
                // longer than the session timeout, and never carries over midnight
                var startsNewSession = current == null
                                       || candidate.Start - current.EndTimeUtc!.Value > _sessionTimeout
                                       || activity.StartTime.Date != current.StartTime.Date;
                if (startsNewSession)
                {
                    current = new AppUserReadingSession
                    {
                        AppUserId = userId,
                        IsActive = false,
                        IsBackfilled = true,
                        StartTime = activity.StartTime,
                        StartTimeUtc = activity.StartTimeUtc,
                        EndTime = activity.EndTime,
                        EndTimeUtc = activity.EndTimeUtc,
                        ActivityData = [],
                    };
                    sessions.Add(current);
                }

                current!.ActivityData.Add(activity);
                if (activity.EndTimeUtc > current.EndTimeUtc)
                {
                    current.EndTime = activity.EndTime;
                    current.EndTimeUtc = activity.EndTimeUtc;
                }

                results[candidate.Index] = BackfillReadingStatus.Applied;
            }

            context.AppUserReadingSession.AddRange(sessions);
            await context.SaveChangesAsync(ct);

            await MoveProgressAsync(userId, toRecord, scope, ct);

            _logger.LogInformation("Recorded {Count} backfilled reading sessions for user {UserId} from {EntryCount} entries",
                sessions.Count, userId, toRecord.Count);
        }
        finally
        {
            userLock.Release();
        }

        return results;
    }

    private async Task<AppUserReadingSessionActivityData> NewBackfillActivityAsync(BackfillCandidate candidate, BackfillChapter chapter,
        MangaFormat format, IServiceScope scope)
    {
        var entry = candidate.Entry;
        var tz = TimeZoneInfo.Local;
        var startPage = format == MangaFormat.Epub ? entry.StartPage : Math.Max(entry.StartPage - 1, 0);

        var activity = new AppUserReadingSessionActivityData
        {
            ChapterId = chapter.Id,
            VolumeId = chapter.VolumeId,
            SeriesId = chapter.SeriesId,
            LibraryId = chapter.LibraryId,
            Format = format,
            StartPage = startPage,
            EndPage = entry.EndPage,
            StartBookScrollId = entry.StartBookScrollId,
            EndBookScrollId = entry.EndBookScrollId,
            StartTimeUtc = candidate.Start,
            StartTime = TimeZoneInfo.ConvertTimeFromUtc(candidate.Start, tz),
            EndTimeUtc = candidate.End,
            EndTime = TimeZoneInfo.ConvertTimeFromUtc(candidate.End, tz),
            PagesRead = Math.Max(entry.EndPage - startPage, 0),
            TotalPages = chapter.Pages,
            TotalWords = chapter.WordCount,
        };

        if (format == MangaFormat.Epub
            && !string.IsNullOrEmpty(entry.StartBookScrollId) && !string.IsNullOrEmpty(entry.EndBookScrollId))
        {
            activity.WordsRead = await CountWordsAsync(activity, entry, scope);
        }

        return activity;
    }

    private async Task<int> CountWordsAsync(AppUserReadingSessionActivityData activity, BackfillReadingEntryDto entry, IServiceScope scope)
    {
        try
        {
            var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();
            var bookService = scope.ServiceProvider.GetRequiredService<IBookService>();
            var chapter = await cacheService.Ensure(entry.ChapterId);
            if (chapter == null) return 0;

            return await bookService.GetWordCountBetweenXPaths(
                cacheService.GetCachedFile(chapter),
                entry.StartBookScrollId!,
                activity.StartPage,
                entry.EndBookScrollId!,
                entry.EndPage);
        }
        catch (Exception ex)
        {
            // Words are a nicety; the reading is worth recording without them
            _logger.LogError(ex, "Error calculating words read for backfilled activity on chapter {ChapterId}", entry.ChapterId);
            return 0;
        }
    }

    /// <summary>
    /// A chapter's progress moves to where its latest backfilled reading ended, unless progress was saved after that
    /// (the user kept reading on another device). Chapters that were finished count as read once more, like when a live
    /// session closes.
    /// </summary>
    private async Task MoveProgressAsync(int userId, List<BackfillCandidate> recorded, IServiceScope scope, CancellationToken ct)
    {
        var context = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var readerService = scope.ServiceProvider.GetRequiredService<IReaderService>();

        foreach (var latest in recorded.GroupBy(c => c.Entry.ChapterId).Select(g => g.MaxBy(c => c.End)!))
        {
            var progress = await unitOfWork.AppUserProgressRepository.GetUserProgressAsync(latest.Entry.ChapterId, userId, ct);
            if (progress != null && progress.LastModifiedUtc >= latest.End) continue;

            await readerService.SaveReadingProgress(new ProgressDto
            {
                ChapterId = latest.Entry.ChapterId,
                PageNum = latest.Entry.EndPage,
                BookScrollId = latest.Entry.EndBookScrollId,
                VolumeId = 0,
                SeriesId = 0,
                LibraryId = 0,
                LastModifiedUtc = latest.End,
            }, userId, saveToReadingSession: false, ct: ct);
        }

        var completed = recorded
            .Where(c => c.Entry.EndPage > 0)
            .Select(c => c.Entry.ChapterId)
            .Distinct()
            .ToList();
        if (completed.Count == 0) return;

        var finished = await context.Chapter
            .Where(c => completed.Contains(c.Id) && c.Pages > 0)
            .Select(c => new { c.Id, c.Pages })
            .ToListAsync(ct);
        var finishedIds = finished
            .Where(c => recorded.Any(r => r.Entry.ChapterId == c.Id && r.Entry.EndPage >= c.Pages))
            .Select(c => c.Id)
            .ToList();
        if (finishedIds.Count == 0) return;

        await context.AppUserProgresses
            .Where(p => p.AppUserId == userId && finishedIds.Contains(p.ChapterId))
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.TotalReads, x => x.TotalReads + 1), ct);
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    /// <summary>Whole seconds, so a time that went through JSON and the database still equals itself</summary>
    private static DateTime Truncate(DateTime value) => new(value.Ticks - value.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);

    private sealed record BackfillCandidate(int Index, BackfillReadingEntryDto Entry, DateTime Start, DateTime End);

    private sealed record BackfillChapter(int Id, int VolumeId, int SeriesId, int LibraryId, int Pages, long WordCount);

    public static Dictionary<int, (DateTime Start, DateTime End)> ScheduleChapters(
        Dictionary<int, HourEstimateRangeDto> estimatedHoursByChapter, DateTime currentEnd)
    {
        var schedule = new Dictionary<int, (DateTime Start, DateTime End)>();

        foreach (var (chapterId, estimateRangeDto) in estimatedHoursByChapter)
        {
            // Don't allow chapters to go over 24h.
            var actualEstimate = Math.Min(estimateRangeDto.AvgHours, 24);

            var duration = TimeSpan.FromHours(actualEstimate);
            var start = currentEnd - duration;

            // If start is on a different day, push the whole chapter to end at midnight of current End's day
            if (start.Date < currentEnd.Date)
            {
                var midnight = currentEnd.Date; // midnight = start of currentEnd's date
                start = midnight - duration;
                currentEnd = midnight;
            }

            schedule[chapterId] = (start, currentEnd);
            currentEnd = start;
        }

        return schedule;
    }


    private async Task<AppUserReadingSession> GetOrCreateSessionAsync(int userId, ProgressDto dto, IDataContext context)
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
            _logger.LogDebug("Found existing session {SessionId} for user {UserId} for Chapter {ChapterId}", existingSession.Id, userId, dto.ChapterId);
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

        _logger.LogDebug("Created new session {SessionId} for user {UserId} for Chapter {ChapterId}", newSession.Id, userId, dto.ChapterId);
        return newSession;
    }

    private async Task UpdateActivityDataAsync(AppUserReadingSession session, ProgressDto progressDto, ClientInfoData? clientInfo,
        int? deviceId, IServiceScope scope, IDataContext context)
    {
        var cutoffUtc = DateTime.UtcNow - _sessionTimeout;

        var existingActivity = session.ActivityData
            .Where(d => d.EndTimeUtc == null || d.EndTimeUtc >= cutoffUtc) // End time works as a LastModified
            .FirstOrDefault(d => d.ChapterId == progressDto.ChapterId);

        var chapterFormat = await GetChapterFormatAsync(progressDto.ChapterId, context);

        if (existingActivity != null)
        {
            _logger.LogDebug("Updating Session {SessionId} with an existing Activity {ActivityId}", session.Id,  existingActivity.Id);
            await UpdateExistingActivityAsync(existingActivity, progressDto, clientInfo, deviceId, chapterFormat, scope);
        }
        else
        {
            _logger.LogDebug("Updating Session {SessionId} with a new Activity", session.Id);
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

    private async Task UpdateExistingActivityAsync(AppUserReadingSessionActivityData activity, ProgressDto progressDto, ClientInfoData? clientInfo,
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

    private async Task UpdateEpubActivityAsync(AppUserReadingSessionActivityData activity, ProgressDto progressDto, Chapter chapter,
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
            var context = scope.ServiceProvider.GetRequiredService<IDataContext>();
            var eventHub = scope.ServiceProvider.GetRequiredService<IEventHub>();

            var expiredSessions = await context.AppUserReadingSession
                .Where(s => s.IsActive)
                .Where(s => s.LastModifiedUtc < cutoffUtc || s.StartTime < midnightToday)
                .Include(s => s.ActivityData)
                .ToListAsync();

            if (expiredSessions.Count == 0) return;

            _logger.LogInformation("Closing {Count} expired reading sessions", expiredSessions.Count);

            // Collect all chapter IDs to remove from cache
            var chapterIdsToInvalidate = expiredSessions
                .SelectMany(s => s.ActivityData.Select(a => a.ChapterId))
                .Distinct()
                .ToList();

            var allCompletedChapterIds = new List<int>();

            foreach (var session in expiredSessions)
            {
                _logger.LogDebug("Closing session {SessionId} for user {UserId}", session.Id, session.AppUserId);
                var completedIds = CloseSession(session);
                allCompletedChapterIds.AddRange(completedIds);
            }

            await context.SaveChangesAsync();

            // Batch cache invalidation after all sync work
            foreach (var chapterId in chapterIdsToInvalidate)
            {
                await _cache.RemoveAsync(GetChapterFormatCacheKey(chapterId));
            }

            // Batch notify after save
            foreach (var session in expiredSessions)
            {
                await eventHub.SendMessageAsync(
                    MessageFactory.ReadingSessionClose,
                    MessageFactory.ReadingSessionCloseEvent(session.AppUserId, session.Id));
            }

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

    private List<int> CloseSession(AppUserReadingSession session)
    {
        var lastActivity = session.ActivityData
            .Where(ad => ad.EndTime.HasValue)
            .MaxBy(ad => ad.EndTime);

        var endTime = lastActivity?.EndTime ?? session.LastModified;
        var endTimeUtc = lastActivity?.EndTimeUtc ?? session.LastModifiedUtc;

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

        var completedChapterIds = session.ActivityData
            .Where(d => d.TotalPages > 0 && d.EndPage >= d.TotalPages)
            .Select(d => d.ChapterId)
            .Distinct()
            .ToList();

        _logger.LogDebug(
            "Closed session {SessionId} for user {UserId}, {ActivityCount} activities, {CompletedCount} completed chapters",
            session.Id, session.AppUserId, session.ActivityData.Count, completedChapterIds.Count);

        return completedChapterIds;
    }

    private async Task<MangaFormat> GetChapterFormatAsync(int chapterId, IDataContext context)
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

        return new AppUserReadingSessionActivityData(dto, startPage, format);
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
