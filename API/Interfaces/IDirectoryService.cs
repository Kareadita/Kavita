using System.Collections.Generic;

namespace API.Interfaces
{
    public interface IDirectoryService
    {
        /// <summary>
        /// Lists out top-level folders for a given directory. Filters out System and Hidden folders.
        /// </summary>
        /// <param name="rootPath">Absolute path of directory to scan.</param>
        /// <returns>List of folder names</returns>
        IEnumerable<string> ListDirectory(string rootPath);

        /// <summary>
        /// Lists out top-level files for a given directory.
        /// TODO: Implement ability to provide a filter for file types
        /// </summary>
        /// <param name="rootPath">Absolute path </param>
        /// <returns>List of folder names</returns>
        IEnumerable<string> ListFiles(string rootPath);

        /// <summary>
        /// Given a library id, scans folders for said library. Parses files and generates DB updates. Will overwrite
        /// cover images if forceUpdate is true.
        /// </summary>
        /// <param name="libraryId">Library to scan against</param>
        /// <param name="forceUpdate">Force overwriting for cover images</param>
        void ScanLibrary(int libraryId, bool forceUpdate);

        /// <summary>
        /// Extracts an archive to a temp cache directory. Returns path to new directory. If temp cache directory already exists,
        /// will return that without performing an extraction. Returns empty string if there are any invalidations which would
        /// prevent operations to perform correctly (missing archivePath file, empty archive, etc).
        /// </summary>
        /// <param name="archivePath">A valid file to an archive file.</param>
        /// <param name="volumeId">Id of volume being extracted.</param>
        /// <returns></returns>
        string ExtractArchive(string archivePath, int volumeId);
        
    }
}