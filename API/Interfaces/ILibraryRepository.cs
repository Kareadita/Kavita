using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;

namespace API.Interfaces
{
    public interface ILibraryRepository
    {
        void Update(Library library);
        Task<IEnumerable<LibraryDto>> GetLibraryDtosAsync();
        Task<bool> LibraryExists(string libraryName);
        Task<Library> GetLibraryForIdAsync(int libraryId);
        Task<IEnumerable<LibraryDto>> GetLibraryDtosForUsernameAsync(string userName);
        Task<Library> GetLibraryForNameAsync(string libraryName);
        Task<bool> DeleteLibrary(int libraryId);
    }
}