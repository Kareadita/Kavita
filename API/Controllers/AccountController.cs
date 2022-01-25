﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.Account;
using API.DTOs.Email;
using API.Entities;
using API.Errors;
using API.Extensions;
using API.Services;
using AutoMapper;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Controllers
{
    /// <summary>
    /// All Account matters
    /// </summary>
    public class AccountController : BaseApiController
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly ITokenService _tokenService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<AccountController> _logger;
        private readonly IMapper _mapper;
        private readonly IAccountService _accountService;
        private readonly IEmailService _emailService;

        /// <inheritdoc />
        public AccountController(UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            ITokenService tokenService, IUnitOfWork unitOfWork,
            ILogger<AccountController> logger,
            IMapper mapper, IAccountService accountService, IEmailService emailService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _mapper = mapper;
            _accountService = accountService;
            _emailService = emailService;
        }

        /// <summary>
        /// Update a user's password
        /// </summary>
        /// <param name="resetPasswordDto"></param>
        /// <returns></returns>
        [HttpPost("reset-password")]
        public async Task<ActionResult> UpdatePassword(ResetPasswordDto resetPasswordDto)
        {
            _logger.LogInformation("{UserName} is changing {ResetUser}'s password", User.GetUsername(), resetPasswordDto.UserName);
            var user = await _userManager.Users.SingleAsync(x => x.UserName == resetPasswordDto.UserName);

            if (resetPasswordDto.UserName != User.GetUsername() && !User.IsInRole(PolicyConstants.AdminRole))
                return Unauthorized("You are not permitted to this operation.");

            var errors = await _accountService.ChangeUserPassword(user, resetPasswordDto.Password);
            if (errors.Any())
            {
                return BadRequest(errors);
            }

            _logger.LogInformation("{User}'s Password has been reset", resetPasswordDto.UserName);
            return Ok();
        }

        /// <summary>
        /// Register a new user on the server
        /// </summary>
        /// <param name="registerDto"></param>
        /// <returns></returns>
        [HttpPost("register")]
        public async Task<ActionResult<UserDto>> Register(RegisterDto registerDto)
        {
            try
            {
                var usernameValidation = await _accountService.ValidateUsername(registerDto.Username);
                if (usernameValidation.Any())
                {
                    return BadRequest(usernameValidation);
                }

                // If we are registering an admin account, ensure there are no existing admins or user registering is an admin
                if (registerDto.IsAdmin)
                {
                    var firstTimeFlow = !(await _userManager.GetUsersInRoleAsync("Admin")).Any();
                    if (!firstTimeFlow && !await _unitOfWork.UserRepository.IsUserAdminAsync(
                            await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername())))
                    {
                        return BadRequest("You are not permitted to create an admin account");
                    }
                }

                var user = _mapper.Map<AppUser>(registerDto);
                user.UserPreferences ??= new AppUserPreferences();
                user.ApiKey = HashUtil.ApiKey();

                var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
                if (!settings.EnableAuthentication && !registerDto.IsAdmin)
                {
                    _logger.LogInformation("User {UserName} is being registered as non-admin with no server authentication. Using default password", registerDto.Username);
                    registerDto.Password = AccountService.DefaultPassword;
                }

                var result = await _userManager.CreateAsync(user, registerDto.Password);

                if (!result.Succeeded) return BadRequest(result.Errors);


                var role = registerDto.IsAdmin ? PolicyConstants.AdminRole : PolicyConstants.PlebRole;
                var roleResult = await _userManager.AddToRoleAsync(user, role);

                if (!roleResult.Succeeded) return BadRequest(result.Errors);

                // When we register an admin, we need to grant them access to all Libraries.
                if (registerDto.IsAdmin)
                {
                    _logger.LogInformation("{UserName} is being registered as admin. Granting access to all libraries",
                        user.UserName);
                    var libraries = (await _unitOfWork.LibraryRepository.GetLibrariesAsync()).ToList();
                    foreach (var lib in libraries)
                    {
                        lib.AppUsers ??= new List<AppUser>();
                        lib.AppUsers.Add(user);
                    }

                    if (libraries.Any() && !await _unitOfWork.CommitAsync())
                        _logger.LogError("There was an issue granting library access. Please do this manually");
                }

                return new UserDto
                {
                    Username = user.UserName,
                    Token = await _tokenService.CreateToken(user),
                    RefreshToken = await _tokenService.CreateRefreshToken(user),
                    ApiKey = user.ApiKey,
                    Preferences = _mapper.Map<UserPreferencesDto>(user.UserPreferences)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Something went wrong when registering user");
                await _unitOfWork.RollbackAsync();
            }

            return BadRequest("Something went wrong when registering user");
        }

        /// <summary>
        /// Perform a login. Will send JWT Token of the logged in user back.
        /// </summary>
        /// <param name="loginDto"></param>
        /// <returns></returns>
        [HttpPost("login")]
        public async Task<ActionResult<UserDto>> Login(LoginDto loginDto)
        {
            var user = await _userManager.Users
                .Include(u => u.UserPreferences)
                .SingleOrDefaultAsync(x => x.NormalizedUserName == loginDto.Username.ToUpper());

            if (user == null) return Unauthorized("Invalid username");

            // TODO: Need to check if email has been confirmed or not

            var isAdmin = await _unitOfWork.UserRepository.IsUserAdminAsync(user);
            var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
            if (!settings.EnableAuthentication && !isAdmin)
            {
                _logger.LogDebug("User {UserName} is logging in with authentication disabled", loginDto.Username);
                loginDto.Password = AccountService.DefaultPassword;
            }

            var result = await _signInManager
                .CheckPasswordSignInAsync(user, loginDto.Password, false);

            if (!result.Succeeded) return Unauthorized("Your credentials are not correct.");

            // Update LastActive on account
            user.LastActive = DateTime.Now;
            user.UserPreferences ??= new AppUserPreferences();

            _unitOfWork.UserRepository.Update(user);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("{UserName} logged in at {Time}", user.UserName, user.LastActive);

            return new UserDto
            {
                Username = user.UserName,
                Token = await _tokenService.CreateToken(user),
                RefreshToken = await _tokenService.CreateRefreshToken(user),
                ApiKey = user.ApiKey,
                Preferences = _mapper.Map<UserPreferencesDto>(user.UserPreferences)
            };
        }

        [HttpPost("refresh-token")]
        public async Task<ActionResult<TokenRequestDto>> RefreshToken([FromBody] TokenRequestDto tokenRequestDto)
        {
            var token = await _tokenService.ValidateRefreshToken(tokenRequestDto);
            if (token == null)
            {
                return Unauthorized(new { message = "Invalid token" });
            }

            return Ok(token);
        }

        /// <summary>
        /// Get All Roles back. See <see cref="PolicyConstants"/>
        /// </summary>
        /// <returns></returns>
        [HttpGet("roles")]
        public ActionResult<IList<string>> GetRoles()
        {
            return typeof(PolicyConstants)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(string))
                .ToDictionary(f => f.Name,
                    f => (string) f.GetValue(null)).Values.ToList();
        }

        /// <summary>
        /// Sets the given roles to the user.
        /// </summary>
        /// <param name="updateRbsDto"></param>
        /// <returns></returns>
        [HttpPost("update-rbs")]
        public async Task<ActionResult> UpdateRoles(UpdateRbsDto updateRbsDto)
        {
            var user = await _userManager.Users
                .Include(u => u.UserPreferences)
                .SingleOrDefaultAsync(x => x.NormalizedUserName == updateRbsDto.Username.ToUpper());
            if (updateRbsDto.Roles.Contains(PolicyConstants.AdminRole) ||
                updateRbsDto.Roles.Contains(PolicyConstants.PlebRole))
            {
                return BadRequest("Invalid Roles");
            }

            var existingRoles = (await _userManager.GetRolesAsync(user))
                .Where(s => s != PolicyConstants.AdminRole && s != PolicyConstants.PlebRole)
                .ToList();

            // Find what needs to be added and what needs to be removed
            var rolesToRemove = existingRoles.Except(updateRbsDto.Roles);
            var result = await _userManager.AddToRolesAsync(user, updateRbsDto.Roles);

            if (!result.Succeeded)
            {
                await _unitOfWork.RollbackAsync();
                return BadRequest("Something went wrong, unable to update user's roles");
            }
            if ((await _userManager.RemoveFromRolesAsync(user, rolesToRemove)).Succeeded)
            {
                return Ok();
            }

            await _unitOfWork.RollbackAsync();
            return BadRequest("Something went wrong, unable to update user's roles");

        }

        /// <summary>
        /// Resets the API Key assigned with a user
        /// </summary>
        /// <returns></returns>
        [HttpPost("reset-api-key")]
        public async Task<ActionResult<string>> ResetApiKey()
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());

            user.ApiKey = HashUtil.ApiKey();

            if (_unitOfWork.HasChanges() && await _unitOfWork.CommitAsync())
            {
                return Ok(user.ApiKey);
            }

            await _unitOfWork.RollbackAsync();
            return BadRequest("Something went wrong, unable to reset key");

        }


        [Authorize(Policy = "RequireAdminRole")]
        [HttpPost("invite")]
        public async Task<ActionResult> InviteUser(InviteUserDto dto)
        {
            var adminUser = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            _logger.LogInformation("{User} is inviting {Email} to the server", adminUser.UserName, dto.Email);

            // Check if there is an existing invite
            var invitedUser = await _unitOfWork.UserRepository.GetUserByEmailAsync(dto.Email);
            if (invitedUser != null)
            {
                if (await _userManager.IsEmailConfirmedAsync(invitedUser))
                    return BadRequest($"User is already registered as {invitedUser.UserName}");
                return BadRequest("User is already invited under this email and has yet to accepted invite.");
            }

            // Create a new user
            var user = new AppUser()
            {
                UserName = dto.Email,
                Email = dto.Email,
                ApiKey = HashUtil.ApiKey(),
                UserPreferences = new AppUserPreferences()
            };

            var result = await _userManager.CreateAsync(user, AccountService.DefaultPassword);
            if (!result.Succeeded) return BadRequest(result.Errors); // TODO: Rollback creation?

            // TODO: Assign Roles

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

            if (string.IsNullOrEmpty(token)) return BadRequest("There was an issue sending email");


            var emailLink = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/registration/confirm-email?token={token}&email={dto.Email}";

            await _emailService.SendConfirmationEmail(new ConfirmationEmailDto()
            {
                EmailAddress = dto.Email,
                InvitingUser = adminUser.UserName,
                ServerConfirmationLink = emailLink
            });
            return Ok(emailLink);
        }

        [HttpPost("confirm-email")]
        public async Task<ActionResult<UserDto>> ConfirmEmail(ConfirmEmailDto dto)
        {
            var user = await _unitOfWork.UserRepository.GetUserByEmailAsync(dto.Email);

            // Validate Password and Username
            var validationErrors = new List<ApiException>();
            validationErrors.AddRange(await _accountService.ValidateUsername(dto.Username));
            validationErrors.AddRange(await _accountService.ValidatePassword(user, dto.Password));

            if (validationErrors.Any())
            {
                // TODO: Figure out how to throw validation errors to UI properly
                return BadRequest(validationErrors);
            }


            var result =  await _userManager.ConfirmEmailAsync(user, dto.Token);
            if (!result.Succeeded)
            {
                // TODO: Throw exceptions
                _logger.LogCritical("Email validation failed");
                if (result.Errors.Any())
                {
                    foreach (var error in result.Errors)
                    {
                        _logger.LogCritical("Email validation error: {Message}", error.Description);
                    }
                }

                return BadRequest("Email validation failed");
            }

            user.UserName = dto.Username;
            var errors = await _accountService.ChangeUserPassword(user, dto.Password);
            if (errors.Any())
            {
                return BadRequest(errors);
            }
            await _unitOfWork.CommitAsync();


            user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(),
                AppUserIncludes.UserPreferences);

            // Perform Login code
            return new UserDto
            {
                Username = user.UserName,
                Token = await _tokenService.CreateToken(user),
                RefreshToken = await _tokenService.CreateRefreshToken(user),
                ApiKey = user.ApiKey,
                Preferences = _mapper.Map<UserPreferencesDto>(user.UserPreferences)
            };
        }


    }
}
