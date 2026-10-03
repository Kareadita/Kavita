using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Kavita.API.Services.SignalR;
using Kavita.Models.DTOs.SignalR;

namespace Kavita.Services.SignalR;

/// <summary>
/// This provides the EventWidget with the replayability and active/up-next jobs.
/// </summary>
/// <remarks>This is singleton and called by many threads - ensure everything is thread-safe</remarks>
/// <param name="timeProvider"></param>
public sealed class ActivityTracker(TimeProvider timeProvider) : IActivityTracker
{
    public const int MaxRows = 50;
    public const int MaxRecentJobs = 100;
    public const int MaxRecentEntries = 200;
    public static readonly TimeSpan NoJobStaleAfter = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan RecentWindow = TimeSpan.FromHours(24);

    private static readonly HashSet<string> EntryMethods =
        [MessageFactory.Info, MessageFactory.Error, MessageFactory.ExternalMatchRateLimitError, MessageFactory.UpdateAvailable];

    private readonly ConcurrentDictionary<string, Row> _rows = new();
    /// <summary>Locks any history activity around _jobs</summary>
    private readonly Lock _historyLock = new();
    private readonly Dictionary<string, JobHistory> _jobs = new();
    private readonly List<(SignalRMessageDto Message, DateTimeOffset Seen)> _entries = [];

    public ActivityTracker() : this(TimeProvider.System)
    {
    }

    private sealed record Row(SignalRMessageDto message, DateTimeOffset FirstSeen, DateTimeOffset LastSeen);

    private sealed class JobHistory(DateTime startedUtc)
    {
        public DateTime StartedUtc { get; } = startedUtc;
        public DateTime LastEventUtc { get; set; } = startedUtc;
        public DateTimeOffset LastSeen { get; set; }
        public Dictionary<string, SignalRMessageDto> Steps { get; } = new();
        public HashSet<string> Ended { get; } = [];
        public int SeriesAdded { get; set; }
        public int SeriesRemoved { get; set; }
    }

    public void Record(string method, SignalRMessageDto message)
    {
        switch (method)
        {
            case MessageFactory.NotificationProgress:
                RecordProgress(message);
                break;
            case MessageFactory.SeriesAdded:
            case MessageFactory.SeriesRemoved:
                CountSeries(method, message.CorrelationId);
                break;
            default:
                if (EntryMethods.Contains(method)) RecordEntry(message);
                break;
        }
    }

    private void RecordProgress(SignalRMessageDto message)
    {
        if (message.Progress == ProgressType.None) return;

        RecordHistory(message);

        // One job sends several progress names (FileScan, ScanProgress, CoverUpdate), each ends on its own
        var key = $"{message.Name}|{message.CorrelationId}";

        if (message.EventType == ProgressEventType.Ended)
        {
            _rows.TryRemove(key, out _);
            return;
        }

        var now = timeProvider.GetUtcNow();
        _rows.AddOrUpdate(key,
            _ => new Row(message, now, now),
            (_, existing) => existing with { message = message, LastSeen = now });

        if (_rows.Count > MaxRows) EvictOldest();
    }

    public IList<SignalRMessageDto> GetRunning(IReadOnlySet<string> processingJobIds)
    {
        var staleBefore = timeProvider.GetUtcNow() - NoJobStaleAfter;

        var deadKeys = _rows
            .Where(r => !IsAlive(r.Value, processingJobIds, staleBefore))
            .Select(r => r.Key)
            .ToList();

        foreach (var key in deadKeys)
        {
            _rows.TryRemove(key, out _);
        }

        return _rows.Values
            .OrderBy(r => r.FirstSeen)
            .Select(r => r.message)
            .ToList();
    }

    public IList<RecentJobDto> GetRecentJobs(IReadOnlySet<string> processingJobIds)
    {
        lock (_historyLock)
        {
            PruneHistory();

            return _jobs
                .Where(j => !processingJobIds.Contains(JobIdOf(j.Key) ?? string.Empty))
                .OrderByDescending(j => j.Value.LastEventUtc)
                .Select(j => new RecentJobDto
                {
                    CorrelationId = j.Key,
                    StartedUtc = j.Value.StartedUtc,
                    EndedUtc = j.Value.LastEventUtc,
                    Steps = j.Value.Steps.Values.ToList(),
                    Completed = j.Value.Steps.Keys.All(j.Value.Ended.Contains),
                    SeriesAdded = j.Value.SeriesAdded,
                    SeriesRemoved = j.Value.SeriesRemoved,
                })
                .ToList();
        }
    }

    public IList<SignalRMessageDto> GetRecentEntries()
    {
        lock (_historyLock)
        {
            PruneHistory();
            return _entries.Select(e => e.Message).Reverse().ToList();
        }
    }

    private void RecordHistory(SignalRMessageDto message)
    {
        if (string.IsNullOrEmpty(message.CorrelationId)) return;

        lock (_historyLock)
        {
            if (!_jobs.TryGetValue(message.CorrelationId, out var job))
            {
                // An ended for a job never seen (or already pruned) has nothing to close
                if (message.EventType == ProgressEventType.Ended) return;

                job = new JobHistory(message.EventTimeUtc);
                _jobs[message.CorrelationId] = job;
            }

            job.LastEventUtc = message.EventTimeUtc;
            job.LastSeen = timeProvider.GetUtcNow();

            if (message.EventType == ProgressEventType.Ended)
            {
                job.Ended.Add(message.Name);
            }
            else
            {
                // CoverUpdate starts again for every series, so a step can reopen
                job.Ended.Remove(message.Name);
                job.Steps[message.Name] = message;
            }

            if (_jobs.Count > MaxRecentJobs) _jobs.Remove(_jobs.MinBy(j => j.Value.LastSeen).Key);
        }
    }

    private void CountSeries(string method, string? correlationId)
    {
        if (string.IsNullOrEmpty(correlationId)) return;

        lock (_historyLock)
        {
            if (!_jobs.TryGetValue(correlationId, out var job)) return;

            if (method == MessageFactory.SeriesAdded) job.SeriesAdded++;
            else job.SeriesRemoved++;
        }
    }

    private void RecordEntry(SignalRMessageDto message)
    {
        lock (_historyLock)
        {
            _entries.Add((message, timeProvider.GetUtcNow()));
            if (_entries.Count > MaxRecentEntries) _entries.RemoveAt(0);
        }
    }

    private void PruneHistory()
    {
        var cutoff = timeProvider.GetUtcNow() - RecentWindow;

        foreach (var key in _jobs.Where(j => j.Value.LastSeen < cutoff).Select(j => j.Key).ToList())
        {
            _jobs.Remove(key);
        }

        _entries.RemoveAll(e => e.Seen < cutoff);
    }

    private static bool IsAlive(Row row, IReadOnlySet<string> processingJobIds, DateTimeOffset staleBefore)
    {
        var jobId = JobIdOf(row.message.CorrelationId);
        return jobId == null ? row.LastSeen >= staleBefore : processingJobIds.Contains(jobId);
    }

    private void EvictOldest()
    {
        var oldest = _rows.MinBy(r => r.Value.LastSeen);
        _rows.TryRemove(oldest.Key, out _);
    }

    private static string? JobIdOf(string? correlationId)
    {
        if (string.IsNullOrEmpty(correlationId)) return null;

        var dot = correlationId.IndexOf('.');
        return dot < 0 ? null : correlationId[(dot + 1)..];
    }
}
