
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
        /// <param name="forceUpdate">Force overwriting for cover images</param>
        Task ScanLibrary(int libraryId, bool forceUpdate);
        void ScanLibraries();
        Task ScanSeries(int libraryId, int seriesId, bool forceUpdate, CancellationToken token);
    }
}
