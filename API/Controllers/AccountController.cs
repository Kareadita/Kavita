using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Constants;
using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
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
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<AccountController> _logger;
        private readonly IMapper _mapper;

        public AccountController(UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager, 
            ITokenService tokenService, IUnitOfWork unitOfWork, 
            ILogger<AccountController> logger,
            IMapper mapper)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _mapper = mapper;
        }

        [Authorize(Policy = "RequireAdminRole")]
        [HttpPost("reset-password")]
        public async Task<ActionResult> UpdatePassword(ResetPasswordDto resetPasswordDto)
        {
            _logger.LogInformation($"{User.GetUsername()} is changing {resetPasswordDto.UserName}'s password.");
            var user = await _userManager.Users.SingleAsync(x => x.UserName == resetPasswordDto.UserName);
            var result = await _userManager.RemovePasswordAsync(user);
            if (!result.Succeeded) return BadRequest("Unable to update password");
            
            result = await _userManager.AddPasswordAsync(user, resetPasswordDto.Password);
            if (!result.Succeeded) return BadRequest("Unable to update password");

            return Ok($"{resetPasswordDto.UserName}'s Password has been reset.");
        }

        [HttpPost("register")]
        public async Task<ActionResult<UserDto>> Register(RegisterDto registerDto)
        {
            if (await _userManager.Users.AnyAsync(x => x.UserName == registerDto.Username))
            {
                return BadRequest("Username is taken.");
            }

            var user = _mapper.Map<AppUser>(registerDto);

            var result = await _userManager.CreateAsync(user, registerDto.Password);

            if (!result.Succeeded) return BadRequest(result.Errors);
            
            var role = registerDto.IsAdmin ? PolicyConstants.AdminRole : PolicyConstants.PlebRole;
            var roleResult = await _userManager.AddToRoleAsync(user, role);

            if (!roleResult.Succeeded) return BadRequest(result.Errors);
            
            // When we register an admin, we need to grant them access to all Libraries.
            if (registerDto.IsAdmin)
            {
                _logger.LogInformation($"{user.UserName} is being registered as admin. Granting access to all libraries.");
                var libraries = await _unitOfWork.LibraryRepository.GetLibrariesAsync();
                foreach (var lib in libraries)
                {
                    lib.AppUsers ??= new List<AppUser>();
                    lib.AppUsers.Add(user);
                }
            }
            
            if (!await _unitOfWork.Complete()) _logger.LogInformation("There was an issue granting library access. Please do this manually.");

            return new UserDto
            {
                Username = user.UserName,
                Token = await _tokenService.CreateToken(user),
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
            _unitOfWork.UserRepository.Update(user);
            await _unitOfWork.Complete();
            
            _logger.LogInformation($"{user.UserName} logged in at {user.LastActive}");

            return new UserDto
            {
                Username = user.UserName,
                Token = await _tokenService.CreateToken(user)
            };
        }
    }
}