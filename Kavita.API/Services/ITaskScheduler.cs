using System;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.Scanner;

namespace Kavita.API.Services;

public interface ITaskScheduler
{
    Task ScheduleTasks(CancellationToken cancellationToken = default);
    Task ScheduleStatsTasks(CancellationToken cancellationToken = default);
    void ScheduleUpdaterTasks();
    Task ScheduleKavitaPlusTasks(CancellationToken cancellationToken = default);
    Task EnqueueScanFolderAsync(ScanFolderRequest request, TimeSpan delay);
    Task EnqueueScanLibrary(int libraryId, bool force = false);
    Task EnqueueScanLibraries(bool force = false);
    void CleanupChapters(int[] chapterIds);
    void RefreshMetadata(int libraryId, bool forceUpdate = true, bool forceColorscape = true);
    Task RefreshSeriesMetadata(int libraryId, int seriesId, bool forceUpdate = false, bool forceColorscape = false);
    Task EnqueueScanSeries(int libraryId, int seriesId, bool forceUpdate = false);
    /// <summary>
    /// Runs the delayed scan asked for first in a minute, moves the rest three hours after it, and drops duplicates
    /// </summary>
    Task RetimeDelayedScans();
    void AnalyzeFilesForSeries(int libraryId, int seriesId, bool forceUpdate = false);
    void CancelStatsTasks();
    Task RunStatCollection();
    void ConvertAllCoversToEncoding();
    Task CleanupDbEntries();
    Task CheckForUpdate(CancellationToken cancellationToken = default);
}
