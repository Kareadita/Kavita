using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;

namespace API.Interfaces
{
    public interface IUserRepository
    {
        void Update(AppUser user);
        void Update(AppUserPreferences preferences);
        public void Delete(AppUser user);
        Task<AppUser> GetUserByUsernameAsync(string username);
        Task<IEnumerable<MemberDto>>  GetMembersAsync();
        Task<IEnumerable<AppUser>> GetAdminUsersAsync();
        Task<AppUserRating> GetUserRating(int seriesId, int userId);
        void AddRatingTracking(AppUserRating userRating);
        Task<AppUserPreferences> GetPreferencesAsync(string username);
        Task<IEnumerable<BookmarkDto>> GetBookmarkDtosForSeries(int userId, int seriesId);
        Task<IEnumerable<BookmarkDto>> GetBookmarkDtosForVolume(int userId, int volumeId);
        Task<IEnumerable<BookmarkDto>> GetBookmarkDtosForChapter(int userId, int chapterId);
        Task<IEnumerable<BookmarkDto>> GetAllBookmarkDtos(int userId);
    }
}
