using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs;

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

        
        //IList<string> ListFiles(string rootPath);

        /// <summary>
        /// Given a library id, scans folders for said library. Parses files and generates DB updates. Will overwrite
        /// cover images if forceUpdate is true.
        /// </summary>
        /// <param name="libraryId">Library to scan against</param>
        /// <param name="forceUpdate">Force overwriting for cover images</param>
        void ScanLibrary(int libraryId, bool forceUpdate);

        /// <summary>
        /// Returns the path a volume would be extracted to.
        /// Deprecated.
        /// </summary>
        /// <param name="volumeId"></param>
        /// <returns></returns>
        string GetExtractPath(int volumeId);

        Task<ImageDto> ReadImageAsync(string imagePath);

        /// <summary>
        /// Extracts an archive to a temp cache directory. Returns path to new directory. If temp cache directory already exists,
        /// will return that without performing an extraction. Returns empty string if there are any invalidations which would
        /// prevent operations to perform correctly (missing archivePath file, empty archive, etc).
        /// </summary>
        /// <param name="archivePath">A valid file to an archive file.</param>
        /// <param name="extractPath">Path to extract to</param>
        /// <returns></returns>
        string ExtractArchive(string archivePath, string extractPath);
        
    }
}