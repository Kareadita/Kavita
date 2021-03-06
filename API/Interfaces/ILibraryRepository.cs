﻿using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;

namespace API.Interfaces
{
    public interface ILibraryRepository
    {
        void Add(Library library);
        void Update(Library library);
        Task<IEnumerable<LibraryDto>> GetLibraryDtosAsync();
        Task<bool> LibraryExists(string libraryName);
        Task<Library> GetLibraryForIdAsync(int libraryId);
        Task<Library> GetFullLibraryForIdAsync(int libraryId);
        Task<IEnumerable<LibraryDto>> GetLibraryDtosForUsernameAsync(string userName);
        Task<IEnumerable<Library>> GetLibrariesAsync();
        Task<bool> DeleteLibrary(int libraryId);
        Task<IEnumerable<Library>> GetLibrariesForUserIdAsync(int userId);
    }
}