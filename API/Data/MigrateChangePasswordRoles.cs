using System.Threading.Tasks;
using API.Constants;
using API.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace API.Data;

public class MigrateChangePasswordRoles
{
    public static async Task Migrate(IUnitOfWork unitOfWork,
        ILogger<Program> logger, UserManager<AppUser> userManager)
    {
        foreach (var user in await unitOfWork.UserRepository.GetAllUsers())
        {
            await userManager.RemoveFromRoleAsync(user, "ChangePassword");
            await userManager.AddToRoleAsync(user, PolicyConstants.ChangePasswordRole);
        }
    }
}
