using System.Collections.Generic;
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

        public void Update(Library library)
        {
            _context.Entry(library).State = EntityState.Modified;
        }

        public async Task<IEnumerable<LibraryDto>> GetLibraryDtosForUsernameAsync(string userName)
        {
            // TODO: Speed this query up
            return await _context.Library
                .Include(l => l.AppUsers)
                .Where(library => library.AppUsers.Any(x => x.UserName == userName))
                .ProjectTo<LibraryDto>(_mapper.ConfigurationProvider)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Library> GetLibraryForNameAsync(string libraryName)
        {
            return await _context.Library
                .Where(x => x.Name == libraryName)
                .Include(f => f.Folders)
                .Include(s => s.Series)
                .SingleAsync();
        }

        public async Task<bool> DeleteLibrary(int libraryId)
        {
            var library = await GetLibraryForIdAsync(libraryId);
            _context.Library.Remove(library);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<IEnumerable<LibraryDto>> GetLibraryDtosAsync()
        {
            return await _context.Library
                .Include(f => f.Folders)
                .ProjectTo<LibraryDto>(_mapper.ConfigurationProvider).ToListAsync();
        }

        public async Task<Library> GetLibraryForIdAsync(int libraryId)
        {
            return await _context.Library
                .Where(x => x.Id == libraryId)
                .Include(f => f.Folders)
                .SingleAsync();
        }
        
        public async Task<bool> LibraryExists(string libraryName)
        {
            return await _context.Library.AnyAsync(x => x.Name == libraryName);
        }

        public async Task<IEnumerable<LibraryDto>> GetLibrariesForUserAsync(AppUser user)
        {
            return await _context.Library.Where(library => library.AppUsers.Contains(user))
                .Include(l => l.Folders)
                .ProjectTo<LibraryDto>(_mapper.ConfigurationProvider).ToListAsync();
        }
    }
}