using System.Threading.Tasks;
using API.Constants;
using API.Entities;
using Microsoft.AspNetCore.Identity;

namespace API.Data;

/// <summary>
/// New role introduced in v0.5.1. Adds the role to all users.
/// </summary>
public static class MigrateChangePasswordRoles
{
    /// <summary>
    /// Will not run if any users have the ChangePassword role already
    /// </summary>
    /// <param name="unitOfWork"></param>
    /// <param name="userManager"></param>
    public static async Task Migrate(IUnitOfWork unitOfWork, UserManager<AppUser> userManager)
    {
        var usersWithRole = await userManager.GetUsersInRoleAsync(PolicyConstants.ChangePasswordRole);
        if (usersWithRole.Count != 0) return;

        var allUsers = await unitOfWork.UserRepository.GetAllUsersAsync();
        foreach (var user in allUsers)
        {
            await userManager.RemoveFromRoleAsync(user, "ChangePassword");
            await userManager.AddToRoleAsync(user, PolicyConstants.ChangePasswordRole);
        }
    }
}
