using System.Collections.Generic;
using API.DTOs;
using API.Entities;

namespace API.Interfaces
{
    public interface IDirectoryService
    {
        IEnumerable<string> ListDirectory(string rootPath);

        void ScanLibrary(LibraryDto library);
    }
}