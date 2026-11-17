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
    /// match the settings. Rows never probed (no <c>Format</c>) and bookmarks not included.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 2 * 60 * 60), AutomaticRetry(Attempts = 0)]
    Task CleanupImages(CancellationToken ct = default);

}
