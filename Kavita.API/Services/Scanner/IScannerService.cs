using System.Threading.Tasks;
using Kavita.Models.Scanner;

namespace Kavita.API.Services.Scanner;

public interface IScannerService
{
    /// <summary>
    /// Given a library id, scans folders for said library. Parses files and generates DB updates. Will overwrite
    /// cover images if forceUpdate is true.
    /// </summary>
    /// <param name="libraryId">Library to scan against</param>
    /// <param name="forceUpdate">Don't perform optimization checks, defaults to false</param>
    /// <param name="endsJob">No other library is scanned after this, in the same job; sent to the UI on the scan ended event to close the widget row</param>
    Task ScanLibrary(int libraryId, bool forceUpdate = false, bool endsJob = true);

    Task ScanLibraries(bool forceUpdate = false);

    Task ScanSeries(int seriesId, bool bypassFolderOptimizationChecks = true);

    Task ScanFolder(ScanFolderRequest request);
    Task AnalyzeFiles();

}
