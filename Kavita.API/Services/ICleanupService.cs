using System.Threading;
using System.Threading.Tasks;

namespace Kavita.API.Services;

public interface ICleanupService
{
    Task Cleanup(CancellationToken ct = default);
    Task CleanupDbEntries(CancellationToken ct = default);
    Task CleanupCacheAndTempDirectories(CancellationToken ct = default);
    void CleanupCacheDirectory();
    Task DeleteSeriesCoverImages(CancellationToken ct = default);
    Task DeleteChapterCoverImages(CancellationToken ct = default);
    Task DeleteTagCoverImages(CancellationToken ct = default);
    Task CleanupBackups(CancellationToken ct = default);
    Task CleanupLogs(CancellationToken ct = default);
    void CleanupTemp();
    Task EnsureChapterProgressIsCapped(CancellationToken ct = default);
    /// <summary>
    /// Responsible to remove Series from Want To Read when user's have fully read the series and the series has Publication Status of Completed or Cancelled.
    /// </summary>
    /// <returns></returns>
    Task CleanupWantToRead(CancellationToken ct = default);

    Task ConsolidateProgress(CancellationToken ct = default);

    Task CleanupMediaErrors(CancellationToken ct = default);

}
