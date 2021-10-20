using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace API.Interfaces.Services
{
    public interface IDirectoryService
    {
        /// <summary>
        /// Lists out top-level folders for a given directory. Filters out System and Hidden folders.
        /// </summary>
        /// <param name="rootPath">Absolute path of directory to scan.</param>
        /// <returns>List of folder names</returns>
        IEnumerable<string> ListDirectory(string rootPath);

        //string[] GetFilesWithExtension(string path, string searchPatternExpression = "");
        Task<byte[]> ReadFileAsync(string path);
        bool CopyFilesToDirectory(IEnumerable<string> filePaths, string directoryPath, string prepend = "");
        bool Exists(string directory);

        // IEnumerable<string> GetFiles(string path, string searchPatternExpression = "",
        //     SearchOption searchOption = SearchOption.TopDirectoryOnly);

        void CopyFileToDirectory(string fullFilePath, string targetDirectory);
        //public bool CopyDirectoryToDirectory(string sourceDirName, string destDirName, string searchPattern = "*");
    }
}
