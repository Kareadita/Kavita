using System.Threading.Tasks;
using API.Entities;
using API.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    public class AdminController : BaseApiController
    {
        private readonly UserManager<AppUser> _userManager;

        public AdminController(UserManager<AppUser> userManager)
        {
            _userManager = userManager;
        }

        [HttpGet("exists")]
        public async Task<ActionResult<bool>> AdminExists()
        {
            var users = await _userManager.GetUsersInRoleAsync("Admin");
            return users.Count > 0;
        }

        
        
        
    }
}