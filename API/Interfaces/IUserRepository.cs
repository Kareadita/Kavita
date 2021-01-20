using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;

namespace API.Interfaces
{
    public interface IUserRepository
    {
        void Update(AppUser user);
        public void Delete(AppUser user);
        Task<AppUser> GetUserByUsernameAsync(string username);
        Task<IEnumerable<MemberDto>>  GetMembersAsync();
        Task<IEnumerable<AppUser>> GetAdminUsersAsync();
        Task<AppUserRating> GetUserRating(int seriesId, int userId);
        void AddRatingTracking(AppUserRating userRating);
    }
}