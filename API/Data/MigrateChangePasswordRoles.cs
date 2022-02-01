using System.Threading.Tasks;
using API.Constants;
using API.Entities;
using Microsoft.AspNetCore.Identity;

namespace API.Data;

public static class MigrateChangePasswordRoles
{
    public static async Task Migrate(IUnitOfWork unitOfWork, UserManager<AppUser> userManager)
    {
        foreach (var user in await unitOfWork.UserRepository.GetAllUsers())
        {
            await userManager.RemoveFromRoleAsync(user, "ChangePassword");
            await userManager.AddToRoleAsync(user, PolicyConstants.ChangePasswordRole);
        }
    }
}
