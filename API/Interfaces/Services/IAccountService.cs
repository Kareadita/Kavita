using System.Collections.Generic;
using System.Threading.Tasks;
using API.Entities;
using API.Errors;

namespace API.Interfaces.Services
{
    public interface IAccountService
    {
        Task<IEnumerable<ApiException>> ChangeUserPassword(AppUser user, string newPassword);
    }
}
