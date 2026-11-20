using System.Threading;
using System.Threading.Tasks;
using Hangfire;

namespace Kavita.API.Services;

public interface ICleanupService
{
    // Hangfire reads filters from the type a job is queued on, and TaskScheduler queues this on the interface
    [AutomaticRetry(Attempts = 3, LogEvents = false, OnAttemptsExceeded = AttemptsExceededAction.Fail, DelaysInSeconds = [120, 300, 300])]
    Task Cleanup(CancellationToken ct = default);
    Task CleanupDbEntries(CancellationToken ct = default);
    Task CleanupCacheAndTempDirectories(CancellationToken ct = default);
    void CleanupCacheDirectory();
    Task CleanupBackups(CancellationToken ct = default);
    Task CleanupLogs(CancellationToken ct = default);
    Task CleanupDebugExports(CancellationToken ct = default);
    Task EnsureChapterProgressIsCapped(CancellationToken ct = default);
    /// <summary>
    /// Responsible to remove Series from Want To Read when user's have fully read the series and the series has Publication Status of Completed or Cancelled.
    /// </summary>
    /// <returns></returns>
    Task CleanupWantToRead(CancellationToken ct = default);

    Task ConsolidateProgress(CancellationToken ct = default);
    /// <summary>
    /// Deletes image rows with no FK (entity deleted/changed) and their files, then re-encodes rows whose format/size no longer
    /// match the settings. Rows never probed (no <c>Format</c>) are not included.
    /// </summary>
    /// <param name="reportSummary">Send the summary to the events widget, for a run the user started</param>
    [DisableConcurrentExecution(timeoutInSeconds: 2 * 60 * 60), AutomaticRetry(Attempts = 0)]
    Task CleanupImages(bool reportSummary, CancellationToken ct = default);
    /// <summary>
    /// One time, queued on every startup by ManualMigrateImageFileBackfillPart2 until it writes that migration's history row.
    /// The v0.9.2 backfill created image rows from the old CoverImage strings without opening the files, so their format
    /// and size are unknown and some names do not match their content. This takes a backup, probes and fixes those rows,
    /// then deletes files no row names.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 2 * 60 * 60), AutomaticRetry(Attempts = 0)]
    Task FinishImageFileMigration(CancellationToken ct = default);

}
