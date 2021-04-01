using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
using API.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data
{
    public class LibraryRepository : ILibraryRepository
    {
        private readonly DataContext _context;
        private readonly IMapper _mapper;

        public LibraryRepository(DataContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }
        
        public void Add(Library library)
        {
            _context.Library.Add(library);
        }

        public void Update(Library library)
        {
            _context.Entry(library).State = EntityState.Modified;
        }

        public async Task<IEnumerable<LibraryDto>> GetLibraryDtosForUsernameAsync(string userName)
        {
            return await _context.Library
                .Include(l => l.AppUsers)
                .Where(library => library.AppUsers.Any(x => x.UserName == userName))
                .ProjectTo<LibraryDto>(_mapper.ConfigurationProvider)
                .AsNoTracking()
                .ToListAsync();
        }
        
        public async Task<IEnumerable<Library>> GetLibrariesAsync()
        {
            return await _context.Library
                .Include(l => l.AppUsers)
                .ToListAsync();
        }

        public async Task<bool> DeleteLibrary(int libraryId)
        {
            var library = await GetLibraryForIdAsync(libraryId);
            _context.Library.Remove(library);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<IEnumerable<Library>> GetLibrariesForUserIdAsync(int userId)
        {
            return await _context.Library
                .Include(l => l.AppUsers)
                .Where(l => l.AppUsers.Select(ap => ap.Id).Contains(userId))
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<IEnumerable<LibraryDto>> GetLibraryDtosAsync()
        {
            return await _context.Library
                .Include(f => f.Folders)
                .OrderBy(l => l.Name)
                .ProjectTo<LibraryDto>(_mapper.ConfigurationProvider)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Library> GetLibraryForIdAsync(int libraryId)
        {
            return await _context.Library
                .Where(x => x.Id == libraryId)
                .Include(f => f.Folders)
                .Include(l => l.Series)
                .SingleAsync();
        }
        /// <summary>
        /// This returns a Library with all it's Series -> Volumes -> Chapters. This is expensive. Should only be called when needed.
        /// </summary>
        /// <param name="libraryId"></param>
        /// <returns></returns>
        public async Task<Library> GetFullLibraryForIdAsync(int libraryId)
        {
            return await _context.Library
                .Where(x => x.Id == libraryId)
                .Include(f => f.Folders)
                .Include(l => l.Series)
                .ThenInclude(s => s.Volumes)
                .ThenInclude(v => v.Chapters)
                .ThenInclude(c => c.Files)
                .AsSplitQuery()
                .SingleAsync();
        }
        
        public async Task<bool> LibraryExists(string libraryName)
        {
            return await _context.Library
                .AsNoTracking()
                .AnyAsync(x => x.Name == libraryName);
        }

        public async Task<IEnumerable<LibraryDto>> GetLibrariesForUserAsync(AppUser user)
        {
            return await _context.Library
                .Where(library => library.AppUsers.Contains(user))
                .Include(l => l.Folders)
                .AsNoTracking()
                .ProjectTo<LibraryDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
        }
        
        
    }
}