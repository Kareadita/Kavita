using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Kavita.API.Services.SignalR;
using Kavita.Models.DTOs.SignalR;

namespace Kavita.Services.SignalR;

public sealed class ActivityTracker(TimeProvider timeProvider) : IActivityTracker
{
    public const int MaxRows = 50;
    public static readonly TimeSpan NoJobStaleAfter = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, Row> _rows = new();

    public ActivityTracker() : this(TimeProvider.System)
    {
    }

    private sealed record Row(SignalRMessageDto MessageDto, DateTimeOffset FirstSeen, DateTimeOffset LastSeen);

    public void Record(SignalRMessageDto messageDto)
    {
        if (messageDto.Progress == ProgressType.None) return;

        // One job sends several progress names (FileScan, ScanProgress, CoverUpdate), each ends on its own
        var key = $"{messageDto.Name}|{messageDto.CorrelationId}";

        if (messageDto.EventType == ProgressEventType.Ended)
        {
            _rows.TryRemove(key, out _);
            return;
        }

        var now = timeProvider.GetUtcNow();
        _rows.AddOrUpdate(key,
            _ => new Row(messageDto, now, now),
            (_, existing) => existing with { MessageDto = messageDto, LastSeen = now });

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
            .Select(r => r.MessageDto)
            .ToList();
    }

    private static bool IsAlive(Row row, IReadOnlySet<string> processingJobIds, DateTimeOffset staleBefore)
    {
        var jobId = JobIdOf(row.MessageDto.CorrelationId);
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
