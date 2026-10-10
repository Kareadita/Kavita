using System;

namespace Kavita.Services.Scanner;

/// <summary>
/// A Hangfire job that is a scan or a scan request.
/// <list type="bullet">
/// <item>A scan is a <see cref="ScannerService"/> ScanLibraries, ScanLibrary or ScanSeries job. It reads files.</item>
/// <item>A scan request is a <see cref="TaskScheduler"/> EnqueueScanLibraries, EnqueueScanLibrary or EnqueueScanSeries
/// job. It reads no files. When it runs, it queues the scan or when the scanner is busy, schedules itself again for
/// later (RetimeDelayedScans moves those).</item>
/// </list>
/// </summary>
/// <param name="IsRequest">A scan request, see above</param>
/// <param name="RunAtUtc">When a Scheduled job is due, null in any other state</param>
public sealed record ScanJob(string JobId, ScanTarget Target, bool Force, bool IsRequest, ScanJobState State,
    DateTime CreatedAtUtc, DateTime? RunAtUtc)
{
    public bool IsWaiting => State is ScanJobState.Scheduled or ScanJobState.Enqueued;
}
