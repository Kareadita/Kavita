using System.Collections.Generic;

namespace API.Interfaces
{
    public interface IDirectoryService
    {
        IEnumerable<string> ListDirectory(string rootPath);

        void ScanLibrary(int libraryId, bool forceUpdate);
    }
}