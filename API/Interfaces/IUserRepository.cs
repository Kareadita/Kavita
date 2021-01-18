using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;

namespace API.Interfaces
{
    public interface IUserRepository
    {
        void Update(AppUser user);
        Task<AppUser> GetUserByUsernameAsync(string username);
        Task<IEnumerable<MemberDto>>  GetMembersAsync();
        public void Delete(AppUser user);
        Task<IEnumerable<AppUser>> GetAdminUsersAsync();
    }
}