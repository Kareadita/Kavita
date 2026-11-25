using System.Threading.Tasks;
using Kavita.Models.Constants;
using Kavita.Models.Entities.User;
using Microsoft.AspNetCore.Identity;

namespace Kavita.Services.Extensions;

public static class UserManagerExtensions
{
    extension(UserManager<AppUser> userManager)
    {
        public async Task<bool> IsEnabledAsync(AppUser user)
        {
            if (await userManager.IsInRoleAsync(user, PolicyConstants.AdminRole))
            {
                return true;
            }

            return await userManager.IsInRoleAsync(user, PolicyConstants.LoginRole);
        }
    }
}
