using System.Threading.Tasks;
using API.Entities;
using API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    public class AdminController : BaseApiController
    {
        private readonly IUserRepository _userRepository;
        private readonly UserManager<AppUser> _userManager;

        public AdminController(IUserRepository userRepository, UserManager<AppUser> userManager)
        {
            _userRepository = userRepository;
            _userManager = userManager;
        }

        [HttpGet("exists")]
        public async Task<ActionResult<bool>> AdminExists()
        {
            var users = await _userManager.GetUsersInRoleAsync("Admin");
            return users.Count > 0;
        }

        [Authorize(Policy = "RequireAdminRole")]
        [HttpDelete]
        public async Task<ActionResult> DeleteUser(string username)
        {
            return BadRequest("Not Implemented");
        }
        
        
    }
}