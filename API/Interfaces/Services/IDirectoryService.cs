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
        /// <summary>
        /// Gets files in a directory. If searchPatternExpression is passed, will match the regex against for filtering.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="searchPatternExpression"></param>
        /// <returns></returns>
        string[] GetFilesWithExtension(string path, string searchPatternExpression = "");
        Task<byte[]> ReadFileAsync(string path);

        /// <summary>
        /// Deletes all files within the directory, then the directory itself.
        /// </summary>
        /// <param name="directoryPath"></param>
        //void ClearAndDeleteDirectory(string directoryPath);
        /// <summary>
        /// Deletes all files within the directory.
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <returns></returns>
        //void ClearDirectory(string directoryPath);

        bool CopyFilesToDirectory(IEnumerable<string> filePaths, string directoryPath);
        bool Exists(string directory);

        IEnumerable<string> GetFiles(string path, string searchPatternExpression = "",
            SearchOption searchOption = SearchOption.TopDirectoryOnly);
    }
}