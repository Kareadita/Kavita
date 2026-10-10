using System;
using System.Collections.Generic;
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

    /// <param name="jobs">Scheduled jobs only, see ScanJobExtensions.Delayed</param>
    public static RetimePlan Plan(IEnumerable<ScanJob> jobs, DateTime nowUtc)
    {
        var byTarget = jobs
            .OrderBy(j => j.CreatedAtUtc)
            .GroupBy(j => new { j.Target, j.Force })
            .ToList();

        var deletes = byTarget.SelectMany(g => g.Skip(1)).Select(j => j.JobId).ToList();
        var kept = byTarget.Select(g => g.First()).ToList();
        if (kept.Count == 0) return new RetimePlan([], deletes);

        // A job already due sooner than a minute is left alone, moving it would only make it later
        var nextRunAt = Min(RunAt(kept[0]), nowUtc + NextScanDelay);
        var queuedRunAt = nextRunAt + QueuedScanDelay;

        var moves = kept
            .Select((job, index) => new { job, runAt = index == 0 ? nextRunAt : queuedRunAt })
            .Where(m => RunAt(m.job) != m.runAt)
            .Select(m => new RetimeMove(m.job.JobId, m.runAt))
            .ToList();

        return new RetimePlan(moves, deletes);
    }

    private static DateTime RunAt(ScanJob job)
    {
        return job.RunAtUtc ?? throw new ArgumentException($"Job {job.JobId} is not scheduled, only delayed jobs can be retimed");
    }

    private static DateTime Min(DateTime a, DateTime b) => a <= b ? a : b;
}
