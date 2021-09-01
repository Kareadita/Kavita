using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.DTOs;
using API.Entities;
using API.Interfaces;
using API.Interfaces.Repositories;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly DataContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IMapper _mapper;

        public UserRepository(DataContext context, UserManager<AppUser> userManager, IMapper mapper)
        {
            _context = context;
            _userManager = userManager;
            _mapper = mapper;
        }

        public void Update(AppUser user)
        {
            _context.Entry(user).State = EntityState.Modified;
        }

        public void Update(AppUserPreferences preferences)
        {
            _context.Entry(preferences).State = EntityState.Modified;
        }

        public void Delete(AppUser user)
        {
            _context.AppUser.Remove(user);
        }

        /// <summary>
        /// Gets an AppUser by username. Returns back Progress information.
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        public async Task<AppUser> GetUserByUsernameAsync(string username)
        {
            return await _context.Users
                .Include(u => u.Progresses)
                .Include(u => u.Bookmarks)
                .SingleOrDefaultAsync(x => x.UserName == username);
        }

        /// <summary>
        /// Gets an AppUser by id. Returns back Progress information.
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        public async Task<AppUser> GetUserByIdAsync(int id)
        {
            return await _context.Users
                .Include(u => u.Progresses)
                .Include(u => u.Bookmarks)
                .SingleOrDefaultAsync(x => x.Id == id);
        }

        public async Task<IEnumerable<AppUser>> GetAdminUsersAsync()
        {
            return await _userManager.GetUsersInRoleAsync(PolicyConstants.AdminRole);
        }

        public async Task<AppUserRating> GetUserRating(int seriesId, int userId)
        {
            return await _context.AppUserRating.Where(r => r.SeriesId == seriesId && r.AppUserId == userId)
                .SingleOrDefaultAsync();
        }

        public void AddRatingTracking(AppUserRating userRating)
        {
            _context.AppUserRating.Add(userRating);
        }

        public async Task<AppUserPreferences> GetPreferencesAsync(string username)
        {
            return await _context.AppUserPreferences
                .Include(p => p.AppUser)
                .SingleOrDefaultAsync(p => p.AppUser.UserName == username);
        }

        public async Task<IEnumerable<BookmarkDto>> GetBookmarkDtosForSeries(int userId, int seriesId)
        {
            return await _context.AppUserBookmark
                .Where(x => x.AppUserId == userId && x.SeriesId == seriesId)
                .OrderBy(x => x.Page)
                .AsNoTracking()
                .ProjectTo<BookmarkDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
        }

        public async Task<IEnumerable<BookmarkDto>> GetBookmarkDtosForVolume(int userId, int volumeId)
        {
            return await _context.AppUserBookmark
                .Where(x => x.AppUserId == userId && x.VolumeId == volumeId)
                .OrderBy(x => x.Page)
                .AsNoTracking()
                .ProjectTo<BookmarkDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
        }

        public async Task<IEnumerable<BookmarkDto>> GetBookmarkDtosForChapter(int userId, int chapterId)
        {
            return await _context.AppUserBookmark
                .Where(x => x.AppUserId == userId && x.ChapterId == chapterId)
                .OrderBy(x => x.Page)
                .AsNoTracking()
                .ProjectTo<BookmarkDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
        }

        public async Task<IEnumerable<BookmarkDto>> GetAllBookmarkDtos(int userId)
        {
            return await _context.AppUserBookmark
                .Where(x => x.AppUserId == userId)
                .OrderBy(x => x.Page)
                .AsNoTracking()
                .ProjectTo<BookmarkDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
        }

        public async Task<AppUser> GetUserByApiKeyAsync(string apiKey)
        {
            return await _context.AppUser
                .SingleOrDefaultAsync(u => u.ApiKey.Equals(apiKey));
        }


        public async Task<IEnumerable<MemberDto>> GetMembersAsync()
        {
            return await _context.Users
                .Include(x => x.Libraries)
                .Include(r => r.UserRoles)
                .ThenInclude(r => r.Role)
                .OrderBy(u => u.UserName)
                .Select(u => new MemberDto
                {
                    Id = u.Id,
                    Username = u.UserName,
                    Created = u.Created,
                    LastActive = u.LastActive,
                    Roles = u.UserRoles.Select(r => r.Role.Name).ToList(),
                    Libraries =  u.Libraries.Select(l => new LibraryDto
                    {
                        Name = l.Name,
                        CoverImage = l.CoverImage,
                        Type = l.Type,
                        Folders = l.Folders.Select(x => x.Path).ToList()
                    }).ToList()
                })
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
