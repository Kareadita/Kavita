using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.Extensions;
using API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Authorize]
    public class UsersController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;

        public UsersController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        
        [Authorize(Policy = "RequireAdminRole")]
        [HttpDelete("delete-user")]
        public async Task<ActionResult> DeleteUser(string username)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(username);
            _unitOfWork.UserRepository.Delete(user);

            if (await _unitOfWork.Complete()) return Ok();

            return BadRequest("Could not delete the user.");
        }
        
        [Authorize(Policy = "RequireAdminRole")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<MemberDto>>> GetUsers()
        {
            return Ok(await _unitOfWork.UserRepository.GetMembersAsync());
        }

        [HttpGet("has-library-access")]
        public async Task<ActionResult<bool>> HasLibraryAccess(int libraryId)
        {
            var libs = await _unitOfWork.LibraryRepository.GetLibraryDtosForUsernameAsync(User.GetUsername());
            return Ok(libs.Any(x => x.Id == libraryId));
        }
    }
}