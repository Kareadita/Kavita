using System.Threading.Tasks;
using API.Constants;
using API.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// Added in v0.7.1.18
/// </summary>
public static class MigrateLoginRoles
{
    /// <summary>
    /// Will not run if any users have the <see cref="PolicyConstants.LoginRole"/> role already
    /// </summary>
    /// <param name="unitOfWork"></param>
    /// <param name="userManager"></param>
    /// <param name="logger"></param>
    public static async Task Migrate(IUnitOfWork unitOfWork, UserManager<AppUser> userManager, ILogger<Program> logger)
    {
        var usersWithRole = await userManager.GetUsersInRoleAsync(PolicyConstants.LoginRole);
        if (usersWithRole.Count != 0) return;

        logger.LogCritical("Running MigrateLoginRoles migration");

        var allUsers = await unitOfWork.UserRepository.GetAllUsersAsync();
        foreach (var user in allUsers)
        {
            await userManager.RemoveFromRoleAsync(user, PolicyConstants.LoginRole);
            await userManager.AddToRoleAsync(user, PolicyConstants.LoginRole);
        }

        logger.LogInformation("MigrateLoginRoles migration complete");
    }
}
