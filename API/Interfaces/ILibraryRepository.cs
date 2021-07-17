using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
using API.Entities.Enums;

namespace API.Interfaces
{
    public interface ILibraryRepository
    {
        void Add(Library library);
        void Update(Library library);
        void Delete(Library library);
        Task<IEnumerable<LibraryDto>> GetLibraryDtosAsync();
        Task<bool> LibraryExists(string libraryName);
        Task<Library> GetLibraryForIdAsync(int libraryId);
        Task<Library> GetFullLibraryForIdAsync(int libraryId);
        Task<Library> GetFullLibraryForIdAsync(int libraryId, int seriesId);
        Task<IEnumerable<LibraryDto>> GetLibraryDtosForUsernameAsync(string userName);
        Task<IEnumerable<Library>> GetLibrariesAsync();
        Task<bool> DeleteLibrary(int libraryId);
        Task<IEnumerable<Library>> GetLibrariesForUserIdAsync(int userId);
        Task<LibraryType> GetLibraryTypeAsync(int libraryId);
    }
}
