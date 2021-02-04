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

        Task<ImageDto> ReadImageAsync(string imagePath);
        /// <summary>
        /// Gets files in a directory. If searchPatternExpression is passed, will match the regex against for filtering.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="searchPatternExpression"></param>
        /// <returns></returns>
        string[] GetFiles(string path, string searchPatternExpression = "");
    }
}