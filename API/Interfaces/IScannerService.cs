using System.Threading.Tasks;
using API.DTOs;

namespace API.Interfaces
{
    public interface IScannerService
    {
        /// <summary>
        /// Given a library id, scans folders for said library. Parses files and generates DB updates. Will overwrite
        /// cover images if forceUpdate is true.
        /// </summary>
        /// <param name="libraryId">Library to scan against</param>
        /// <param name="forceUpdate">Force overwriting for cover images</param>
        void ScanLibrary(int libraryId, bool forceUpdate);

        void ScanLibraries();

        /// <summary>
        /// Performs a forced scan of just a series folder.
        /// </summary>
        /// <param name="libraryId"></param>
        /// <param name="seriesId"></param>
        void ScanSeries(int libraryId, int seriesId);
    }
}