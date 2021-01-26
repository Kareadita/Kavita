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
        string[] GetFiles(string path); // TODO: Refactor. This is currently for CacheServiceTest mocking
    }
}