using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
using Microsoft.AspNetCore.Mvc;

namespace API.Interfaces
{
    public interface ILibraryRepository
    {
        void Update(Library library);
        Task<bool> SaveAllAsync();
        Task<IEnumerable<LibraryDto>> GetLibrariesAsync();
        /// <summary>
        /// Checks to see if a library of the same name exists. We only allow unique library names, no duplicates per LibraryType.
        /// </summary>
        /// <param name="libraryName"></param>
        /// <returns></returns>
        Task<bool> LibraryExists(string libraryName);
        Task<LibraryDto> GetLibraryDtoForIdAsync(int libraryId);
        Task<Library> GetLibraryForIdAsync(int libraryId);
        bool SaveAll();
        Library GetLibraryForName(string libraryName);
        Task<IEnumerable<LibraryDto>> GetLibrariesForUsernameAysnc(string userName);
        //Task<IEnumerable<Series>> GetSeriesForIdAsync(int libraryId);
    }
}