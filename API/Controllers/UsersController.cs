using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers
{
    [Authorize]
    public class UsersController : BaseApiController
    {
        private readonly DataContext _context;
        private readonly IUserRepository _userRepository;
        private readonly ILibraryRepository _libraryRepository;

        public UsersController(DataContext context, IUserRepository userRepository, ILibraryRepository libraryRepository)
        {
            _context = context;
            _userRepository = userRepository;
            _libraryRepository = libraryRepository;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MemberDto>>> GetUsers()
        {
            return Ok(await _userRepository.GetMembersAsync());
        }
        
        [HttpPost("add-library")]
        public async Task<ActionResult> AddLibrary(CreateLibraryDto createLibraryDto)
        {
            //_logger.Log(LogLevel.Debug, "Creating a new " + createLibraryDto.Type + " library");
            var user = await _userRepository.GetUserByUsernameAsync(User.GetUsername());

            if (user == null) return BadRequest("Could not validate user");
            
            
            if (await _libraryRepository.LibraryExists(createLibraryDto.Name))
            {
                return BadRequest("Library name already exists. Please choose a unique name to the server.");
            }
            
            // TODO: We probably need to clean the folders before we insert
            var library = new Library
            {
                Name = createLibraryDto.Name,
                Type = createLibraryDto.Type,
                //Folders = createLibraryDto.Folders
                AppUsers = new List<AppUser>() { user }
            };

            user.Libraries ??= new List<Library>(); // If user is null, then set it
            
            user.Libraries.Add(library);
            //_userRepository.Update(user);

            if (await _userRepository.SaveAllAsync())
            {
                return Ok();
            }
            
            return BadRequest("Not implemented");
        }
    }
}