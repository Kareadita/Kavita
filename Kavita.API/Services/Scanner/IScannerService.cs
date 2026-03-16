using System.Threading.Tasks;
using Hangfire;
using Kavita.Common.Constants;
using Kavita.Models.Constants;

namespace Kavita.API.Services.Scanner;

public interface IScannerService
{
    /// <summary>
    /// Given a library id, scans folders for said library. Parses files and generates DB updates. Will overwrite
    /// cover images if forceUpdate is true.
    /// </summary>
    /// <param name="libraryId">Library to scan against</param>
    /// <param name="forceUpdate">Don't perform optimization checks, defaults to false</param>
    [Queue(TaskSchedulerConstants.ScanQueue)]
    [DisableConcurrentExecution(60 * 60 * 60)]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    Task ScanLibrary(int libraryId, bool forceUpdate = false, bool isSingleScan = true);

    [Queue(TaskSchedulerConstants.ScanQueue)]
    [DisableConcurrentExecution(60 * 60 * 60)]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    Task ScanLibraries(bool forceUpdate = false);

    [Queue(TaskSchedulerConstants.ScanQueue)]
    [DisableConcurrentExecution(60 * 60 * 60)]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    Task ScanSeries(int seriesId, bool bypassFolderOptimizationChecks = true);

    Task ScanFolder(string folder, string originalPath, bool abortOnNoSeriesMatch = false);
    Task AnalyzeFiles();

}
