using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Authorize]
    public class UsersController : BaseApiController
    {
        private readonly IUserRepository _userRepository;
        private readonly ILibraryRepository _libraryRepository;

        public UsersController(IUserRepository userRepository, ILibraryRepository libraryRepository)
        {
            _userRepository = userRepository;
            _libraryRepository = libraryRepository;
        }
        
        [Authorize(Policy = "RequireAdminRole")]
        [HttpDelete("delete-user")]
        public async Task<ActionResult> DeleteUser(string username)
        {
            var user = await _userRepository.GetUserByUsernameAsync(username);
            _userRepository.Delete(user);

            if (await _userRepository.SaveAllAsync())
            {
                return Ok();
            }
            
            return BadRequest("Could not delete the user.");
        }
        
        [Authorize(Policy = "RequireAdminRole")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<MemberDto>>> GetUsers()
        {
            return Ok(await _userRepository.GetMembersAsync());
        }

        [HttpGet("has-library-access")]
        public async Task<ActionResult<bool>> HasLibraryAccess(int libraryId)
        {
            var user = await _userRepository.GetUserByUsernameAsync(User.GetUsername());

            if (user == null) return BadRequest("Could not validate user");

            var libs = await _libraryRepository.GetLibrariesForUsernameAysnc(user.UserName);

            return Ok(libs.Any(x => x.Id == libraryId));
        }
    }
}