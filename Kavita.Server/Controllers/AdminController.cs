using System.Threading.Tasks;
using Kavita.API.Attributes;
using Kavita.Models.Constants;
using Kavita.Models.Entities.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Kavita.Server.Controllers;

#nullable enable

public class AdminController(UserManager<AppUser> userManager) : BaseApiController
{
    /// <summary>
    /// Checks if an admin exists on the system. This is essentially a check to validate if the system has been set up.
    /// </summary>
    /// <returns></returns>
    [AllowAnonymous]
    [SkipDeviceTracking]
    [HttpGet("exists")]
    public async Task<ActionResult<bool>> AdminExists()
    {
        var users = await userManager.GetUsersInRoleAsync(PolicyConstants.AdminRole);
        return users.Count > 0;
    }


}
