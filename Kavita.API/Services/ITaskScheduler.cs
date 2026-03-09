using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kavita.API.Services;

public interface ITaskScheduler
{
    Task ScheduleTasks(CancellationToken cancellationToken = default);
    Task ScheduleStatsTasks(CancellationToken cancellationToken = default);
    void ScheduleUpdaterTasks();
    Task ScheduleKavitaPlusTasks(CancellationToken cancellationToken = default);
    void ScanFolder(string folderPath, string originalPath, TimeSpan delay);
    void ScanFolder(string folderPath, bool abortOnNoSeriesMatch = false);
    Task ScanLibrary(int libraryId, bool force = false);
    Task ScanLibraries(bool force = false);
    void CleanupChapters(int[] chapterIds);
    void RefreshMetadata(int libraryId, bool forceUpdate = true, bool forceColorscape = true);
    Task RefreshSeriesMetadata(int libraryId, int seriesId, bool forceUpdate = false, bool forceColorscape = false);
    Task ScanSeries(int libraryId, int seriesId, bool forceUpdate = false);
    void AnalyzeFilesForSeries(int libraryId, int seriesId, bool forceUpdate = false);
    void CancelStatsTasks();
    Task RunStatCollection();
    void ConvertAllCoversToEncoding();
    Task CleanupDbEntries();
    Task CheckForUpdate(CancellationToken cancellationToken = default);
}
