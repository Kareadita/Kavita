using System;
using System.Threading.Tasks;

namespace Kavita.API.Services;

public interface ITaskScheduler
{
    Task ScheduleTasks();
    Task ScheduleStatsTasks();
    void ScheduleUpdaterTasks();
    Task ScheduleKavitaPlusTasks();
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
    void CovertAllCoversToEncoding();
    Task CleanupDbEntries();
    Task CheckForUpdate();
    Task SyncThemes();
}
