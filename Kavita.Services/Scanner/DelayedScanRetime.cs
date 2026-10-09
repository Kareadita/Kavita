using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Kavita.Services.Scanner;

/// <summary>
/// Decides the new run times of delayed scans when a scan ends. The scan asked for first runs next, the rest wait
/// <see cref="QueuedScanDelay"/> after it, and a second job for the same target is dropped
/// </summary>
public static class DelayedScanRetime
{
    public static readonly TimeSpan NextScanDelay = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan QueuedScanDelay = TimeSpan.FromHours(3);

    public static RetimePlan Plan(IEnumerable<DelayedScanJob> jobs, DateTime nowUtc)
    {
        var byTarget = jobs
            .OrderBy(j => j.CreatedAtUtc)
            .GroupBy(TargetKey)
            .ToList();

        var deletes = byTarget.SelectMany(g => g.Skip(1)).Select(j => j.JobId).ToList();
        var kept = byTarget.Select(g => g.First()).ToList();
        if (kept.Count == 0) return new RetimePlan([], deletes);

        // A job already due sooner than a minute is left alone, moving it would only make it later
        var nextRunAt = Min(kept[0].RunAtUtc, nowUtc + NextScanDelay);
        var queuedRunAt = nextRunAt + QueuedScanDelay;

        var moves = kept
            .Select((job, index) => new { job, runAt = index == 0 ? nextRunAt : queuedRunAt })
            .Where(m => m.job.RunAtUtc != m.runAt)
            .Select(m => new RetimeMove(m.job.JobId, m.runAt))
            .ToList();

        return new RetimePlan(moves, deletes);
    }

    private static string TargetKey(DelayedScanJob job)
    {
        return $"{job.Method}({string.Join(',', job.Args.Select(a => Convert.ToString(a, CultureInfo.InvariantCulture)))})";
    }

    private static DateTime Min(DateTime a, DateTime b) => a <= b ? a : b;
}
