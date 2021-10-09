
using System.Threading;
using System.Threading.Tasks;

namespace API.Interfaces.Services
{
    public interface IScannerService
    {
        /// <summary>
        /// Given a library id, scans folders for said library. Parses files and generates DB updates. Will overwrite
        /// cover images if forceUpdate is true.
        /// </summary>
        /// <param name="libraryId">Library to scan against</param>
        Task ScanLibrary(int libraryId);
        Task ScanLibraries();
        Task ScanSeries(int libraryId, int seriesId, CancellationToken token);
    }
}
