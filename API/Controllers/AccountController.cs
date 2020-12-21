using System;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
using API.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Controllers
{
    public class AccountController : BaseApiController
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly ITokenService _tokenService;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<AccountController> _logger;
        private readonly IMapper _mapper;

        public AccountController(UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager, 
            ITokenService tokenService, IUserRepository userRepository, 
            ILogger<AccountController> logger,
            IMapper mapper)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
            _userRepository = userRepository;
            _logger = logger;
            _mapper = mapper;
        }

        [HttpPost("register")]
        public async Task<ActionResult<UserDto>> Register(RegisterDto registerDto)
        {
            _logger.LogInformation("Username: " + registerDto.Password);
            if (await UserExists(registerDto.Username))
            {
                return BadRequest("Username is taken.");
            }

            var user = _mapper.Map<AppUser>(registerDto);

            var result = await _userManager.CreateAsync(user, registerDto.Password);

            if (!result.Succeeded) return BadRequest(result.Errors);

            var roleResult = await _userManager.AddToRoleAsync(user, "Pleb");

            if (!roleResult.Succeeded) return BadRequest(result.Errors);
            
            return new UserDto()
            {
                Username = user.UserName,
                Token = await _tokenService.CreateToken(user),
                IsAdmin = user.IsAdmin
            };
        }

        [HttpPost("login")]
        public async Task<ActionResult<UserDto>> Login(LoginDto loginDto)
        {
            var user = await _userManager.Users
                .SingleOrDefaultAsync(x => x.UserName == loginDto.Username.ToLower());

            if (user == null) return Unauthorized("Invalid username");

            var result = await _signInManager
                .CheckPasswordSignInAsync(user, loginDto.Password, false);

            if (!result.Succeeded) return Unauthorized();
            
            // Update LastActive on account
            user.LastActive = DateTime.Now;
            _userRepository.Update(user);
            await _userRepository.SaveAllAsync();

            return new UserDto()
            {
                Username = user.UserName,
                Token = await _tokenService.CreateToken(user),
                IsAdmin = user.IsAdmin
            };
        }
        
        private async Task<bool> UserExists(string username)
        {
            return await _userManager.Users.AnyAsync(user => user.UserName == username.ToLower());
        }
    }
}