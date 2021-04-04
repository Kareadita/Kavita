
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
        void ScanLibrary(int libraryId, bool forceUpdate);
        /// <summary>
        /// Behaves just like ScanLibrary but only applies to the folders where files are located. If in root directory,
        /// only existing files will be updated.
        /// </summary>
        /// <param name="seriesId"></param>
        /// <param name="forceUpdate"></param>
        void ScanSeries(int seriesId, bool forceUpdate);

        void ScanLibraries();
    }
}