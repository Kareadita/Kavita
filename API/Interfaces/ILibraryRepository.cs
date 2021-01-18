using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;

namespace API.Interfaces
{
    public interface ILibraryRepository
    {
        void Update(Library library);
        Task<IEnumerable<LibraryDto>> GetLibrariesAsync();
        Task<bool> LibraryExists(string libraryName);
        Task<Library> GetLibraryForIdAsync(int libraryId);
        bool SaveAll();
        Task<IEnumerable<LibraryDto>> GetLibrariesDtoForUsernameAsync(string userName);
        Task<Library> GetLibraryForNameAsync(string libraryName);

        Task<bool> DeleteLibrary(int libraryId);
    }
}