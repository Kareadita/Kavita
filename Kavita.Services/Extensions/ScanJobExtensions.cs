using System;
using System.Collections.Generic;
using System.Linq;
using Kavita.Services.Scanner;

namespace Kavita.Services.Extensions;

/// <summary>
/// Questions about a <see cref="ScanJobQueue.Read"/> snapshot. A scan reads files, a scan request is a TaskScheduler
/// EnqueueScan* job that queues one, see <see cref="ScanJob"/>
/// </summary>
public static class ScanJobExtensions
{
    /// <summary>
    /// A scan is queued or reading files. Scan requests do not count
    /// </summary>
    public static bool IsScannerBusy(this IEnumerable<ScanJob> jobs)
    {
        return jobs.Any(j => !j.IsRequest && j.State is ScanJobState.Enqueued or ScanJobState.Processing);
    }

    /// <summary>
    /// A job that has not started and will read the target, forced when <paramref name="force"/> asks for it.
    /// A scan already reading files never counts, it may have passed the change
    /// </summary>
    public static ScanJob? FindWaiting(this IEnumerable<ScanJob> jobs, ScanTarget target, bool force = false)
    {
        return jobs.FirstOrDefault(j => j.IsWaiting && j.Target.Covers(target) && (j.Force || !force));
    }

    /// <summary>
    /// Like <see cref="FindWaiting"/>, and also a scan request that started and is about to queue its scan
    /// </summary>
    public static ScanJob? FindAlreadyRequested(this IEnumerable<ScanJob> jobs, ScanTarget target)
    {
        return jobs.FirstOrDefault(j => (j.IsWaiting || j.IsRequest) && j.Target.Covers(target));
    }

    /// <summary>
    /// Scan requests parked in Scheduled because the scanner was busy
    /// </summary>
    public static IEnumerable<ScanJob> Delayed(this IEnumerable<ScanJob> jobs)
    {
        return jobs.Where(j => j.IsRequest && j.State == ScanJobState.Scheduled);
    }

    /// <summary>
    /// A scan waiting in Scheduled, such as an automatic retry, that runs before this time
    /// </summary>
    public static bool HasScanDueBefore(this IEnumerable<ScanJob> jobs, DateTime utc)
    {
        return jobs.Any(j => !j.IsRequest && j.State == ScanJobState.Scheduled && j.RunAtUtc <= utc);
    }

    /// <summary>
    /// A scan of this library, of every library, or of one of its series is queued or reading files
    /// </summary>
    /// <param name="seriesIds">The library's series, a <see cref="ScannerService.ScanSeries"/> job does not carry its library</param>
    public static bool IsLibraryInUse(this IEnumerable<ScanJob> jobs, int libraryId, IReadOnlyCollection<int> seriesIds)
    {
        return jobs
            .Where(j => !j.IsRequest)
            .Select(j => j.Target)
            .Any(t => t.SeriesId is { } seriesId
                ? seriesIds.Contains(seriesId)
                : t.LibraryId is null || t.LibraryId == libraryId);
    }

    /// <summary>
    /// A scan of just this library is queued or reading files. A scan of every library does not count
    /// </summary>
    public static bool HasLibraryScan(this IEnumerable<ScanJob> jobs, int libraryId)
    {
        return jobs.Any(j => !j.IsRequest && j.Target == ScanTarget.Library(libraryId));
    }
}
