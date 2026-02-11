using System.Threading.Tasks;

namespace Kavita.API.Services;

public interface ICleanupService
{
    Task Cleanup();
    Task CleanupDbEntries();
    Task CleanupCacheAndTempDirectories();
    void CleanupCacheDirectory();
    Task DeleteSeriesCoverImages();
    Task DeleteChapterCoverImages();
    Task DeleteTagCoverImages();
    Task CleanupBackups();
    Task CleanupLogs();
    void CleanupTemp();
    Task EnsureChapterProgressIsCapped();
    /// <summary>
    /// Responsible to remove Series from Want To Read when user's have fully read the series and the series has Publication Status of Completed or Cancelled.
    /// </summary>
    /// <returns></returns>
    Task CleanupWantToRead();

    Task ConsolidateProgress();

    Task CleanupMediaErrors();

}
