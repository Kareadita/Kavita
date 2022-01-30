using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using System.Web;
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
using AutoMapper.QueryableExtensions;
using Flurl.Util;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
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
        private readonly IHostEnvironment _environment;

        /// <inheritdoc />
        public AccountController(UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            ITokenService tokenService, IUnitOfWork unitOfWork,
            ILogger<AccountController> logger,
            IMapper mapper, IAccountService accountService, IEmailService emailService, IHostEnvironment environment)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _mapper = mapper;
            _accountService = accountService;
            _emailService = emailService;
            _environment = environment;
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
        /// Register the first user (admin) on the server. Will not do anything if an admin is already confirmed
        /// </summary>
        /// <param name="registerDto"></param>
        /// <returns></returns>
        [HttpPost("register")]
        public async Task<ActionResult<UserDto>> RegisterFirstUser(RegisterDto registerDto)
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            if (admins.Count > 0) return BadRequest("Not allowed");

            try
            {
                var usernameValidation = await _accountService.ValidateUsername(registerDto.Username);
                if (usernameValidation.Any())
                {
                    return BadRequest(usernameValidation);
                }

                var user = new AppUser()
                {
                    UserName = registerDto.Username,
                    Email = registerDto.Email,
                    UserPreferences = new AppUserPreferences(),
                    ApiKey = HashUtil.ApiKey()
                };

                // I am removing Authentication disabled code
                // var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
                // if (!settings.EnableAuthentication && !registerDto.IsAdmin)
                // {
                //     _logger.LogInformation("User {UserName} is being registered as non-admin with no server authentication. Using default password", registerDto.Username);
                //     registerDto.Password = AccountService.DefaultPassword;
                // }

                var result = await _userManager.CreateAsync(user, registerDto.Password);
                if (!result.Succeeded) return BadRequest(result.Errors);

                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                if (string.IsNullOrEmpty(token)) return BadRequest("There was an issue generating a confirmation token.");
                if (!await ConfirmEmailToken(token, user)) return BadRequest($"There was an issue validating your email: {token}");


                var roleResult = await _userManager.AddToRoleAsync(user, PolicyConstants.AdminRole);
                if (!roleResult.Succeeded) return BadRequest(result.Errors);

                // // When we register an admin, we need to grant them access to all Libraries.
                // if (registerDto.IsAdmin)
                // {
                //     _logger.LogInformation("{UserName} is being registered as admin. Granting access to all libraries",
                //         user.UserName);
                //     var libraries = (await _unitOfWork.LibraryRepository.GetLibrariesAsync()).ToList();
                //     foreach (var lib in libraries)
                //     {
                //         lib.AppUsers ??= new List<AppUser>();
                //         lib.AppUsers.Add(user);
                //     }
                //
                //     if (libraries.Any() && !await _unitOfWork.CommitAsync())
                //         _logger.LogError("There was an issue granting library access. Please do this manually");
                // }

                return new UserDto
                {
                    Username = user.UserName,
                    Email = user.Email,
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

            // Check if the user has an email, if not, inform them so they can migrate
            var validPassword = await _signInManager.UserManager.CheckPasswordAsync(user, loginDto.Password);
            if (string.IsNullOrEmpty(user.Email) && !user.EmailConfirmed && validPassword)
            {
                _logger.LogCritical("User {UserName} does not have an email. Providing a one time migration", user.UserName);
                return Unauthorized(
                    "You are missing an email on your account. Please wait while we migrate your account.");
            }

            var isAdmin = await _unitOfWork.UserRepository.IsUserAdminAsync(user);
            var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
            if (!settings.EnableAuthentication && !isAdmin)
            {
                _logger.LogDebug("User {UserName} is logging in with authentication disabled", loginDto.Username);
                loginDto.Password = AccountService.DefaultPassword;
            }

            var result = await _signInManager
                .CheckPasswordSignInAsync(user, loginDto.Password, false);

            if (!result.Succeeded)
            {
                return Unauthorized(result.IsNotAllowed ? "You must confirm your email first" : "Your credentials are not correct.");
            }

            // Update LastActive on account
            user.LastActive = DateTime.Now;
            user.UserPreferences ??= new AppUserPreferences();

            _unitOfWork.UserRepository.Update(user);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("{UserName} logged in at {Time}", user.UserName, user.LastActive);

            return new UserDto
            {
                Username = user.UserName,
                Email = user.Email,
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

        /// <summary>
        /// Update the user account. This can only affect Username, Email (will require confirming), Roles, and Library access.
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [Authorize(Policy = "RequireAdminRole")]
        [HttpPost("update")]
        public async Task<ActionResult> UpdateAccount(UpdateUserDto dto)
        {
            var adminUser = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            if (!await _unitOfWork.UserRepository.IsUserAdminAsync(adminUser)) return Unauthorized("You do not have permission");

            var user = await _unitOfWork.UserRepository.GetUserByIdAsync(dto.UserId);
            if (user == null) return BadRequest("User does not exist");

            // Check if username is changing
            if (!user.UserName.Equals(dto.Username))
            {
                // Validate username change
                var errors = await _accountService.ValidateUsername(dto.Username);
                if (errors.Any()) return BadRequest("Username already taken");
                user.UserName = dto.Username;
                _unitOfWork.UserRepository.Update(user);
            }

            if (!user.Email.Equals(dto.Email))
            {
                // Validate username change
                var errors = await _accountService.ValidateEmail(dto.Email);
                if (errors.Any()) return BadRequest("Email already registered");
                // NOTE: This needs to be handled differently, like save it in a temp variable in DB until email is validated. For now, I wont allow it
            }

            // Update roles
            var existingRoles = await _userManager.GetRolesAsync(user);
            var hasAdminRole = dto.Roles.Contains(PolicyConstants.AdminRole);
            if (!hasAdminRole)
            {
                dto.Roles.Add(PolicyConstants.PlebRole);
            }
            if (existingRoles.Except(dto.Roles).Any())
            {
                var roles = dto.Roles;

                var roleResult = await _userManager.RemoveFromRolesAsync(user, existingRoles);
                if (!roleResult.Succeeded) return BadRequest(roleResult.Errors);
                roleResult = await _userManager.AddToRolesAsync(user, roles);
                if (!roleResult.Succeeded) return BadRequest(roleResult.Errors);
            }


            var allLibraries = (await _unitOfWork.LibraryRepository.GetLibrariesAsync()).ToList();
            List<Library> libraries;
            if (hasAdminRole)
            {
                _logger.LogInformation("{UserName} is being registered as admin. Granting access to all libraries",
                    user.UserName);
                libraries = allLibraries;
            }
            else
            {
                // Remove user from all libraries
                foreach (var lib in allLibraries)
                {
                    lib.AppUsers ??= new List<AppUser>();
                    lib.AppUsers.Remove(user);
                }

                libraries = (await _unitOfWork.LibraryRepository.GetLibraryForIdsAsync(dto.Libraries)).ToList();
            }

            foreach (var lib in libraries)
            {
                lib.AppUsers ??= new List<AppUser>();
                lib.AppUsers.Add(user);
            }

            if (!_unitOfWork.HasChanges()) return Ok();
            if (await _unitOfWork.CommitAsync())
            {
                return Ok();
            }

            await _unitOfWork.RollbackAsync();
            return BadRequest("There was an exception when updating the user");
        }


        [Authorize(Policy = "RequireAdminRole")]
        [HttpPost("invite")]
        public async Task<ActionResult<string>> InviteUser(InviteUserDto dto)
        {
            var adminUser = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            _logger.LogInformation("{User} is inviting {Email} to the server", adminUser.UserName, dto.Email);

            // Check if there is an existing invite
            var emailValidationErrors = await _accountService.ValidateEmail(dto.Email);
            if (emailValidationErrors.Any())
            {
                var invitedUser = await _unitOfWork.UserRepository.GetUserByEmailAsync(dto.Email);
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

            try
            {
                var result = await _userManager.CreateAsync(user, AccountService.DefaultPassword);
                if (!result.Succeeded) return BadRequest(result.Errors);

                // Assign Roles
                var roles = dto.Roles;
                var hasAdminRole = dto.Roles.Contains(PolicyConstants.AdminRole);
                if (!hasAdminRole)
                {
                    roles.Add(PolicyConstants.PlebRole);
                }

                foreach (var role in roles)
                {
                    if (!PolicyConstants.ValidRoles.Contains(role)) continue;
                    var roleResult = await _userManager.AddToRoleAsync(user, role);
                    if (!roleResult.Succeeded)
                        return
                            BadRequest(roleResult.Errors); // TODO: Combine all these return BadRequest into one big thing
                }

                // Grant access to libraries
                List<Library> libraries;
                if (hasAdminRole)
                {
                    _logger.LogInformation("{UserName} is being registered as admin. Granting access to all libraries",
                        user.UserName);
                    libraries = (await _unitOfWork.LibraryRepository.GetLibrariesAsync()).ToList();
                }
                else
                {
                    libraries = (await _unitOfWork.LibraryRepository.GetLibraryForIdsAsync(dto.Libraries)).ToList();
                }

                foreach (var lib in libraries)
                {
                    lib.AppUsers ??= new List<AppUser>();
                    lib.AppUsers.Add(user);
                }

                await _unitOfWork.CommitAsync();


                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                if (string.IsNullOrEmpty(token)) return BadRequest("There was an issue sending email");

                var host = _environment.IsDevelopment() ? "localhost:4200" : Request.Host.ToString();
                var emailLink =
                    $"{Request.Scheme}://{host}{Request.PathBase}/registration/confirm-email?token={HttpUtility.UrlEncode(token)}&email={HttpUtility.UrlEncode(dto.Email)}";
                _logger.LogInformation("[Invite User]: Email Link: {Link}", emailLink);
                if (dto.SendEmail)
                {
                    await _emailService.SendConfirmationEmail(new ConfirmationEmailDto()
                    {
                        EmailAddress = dto.Email,
                        InvitingUser = adminUser.UserName,
                        ServerConfirmationLink = emailLink
                    });
                }
                return Ok(emailLink);
            }
            catch (Exception ex)
            {
                _unitOfWork.UserRepository.Delete(user);
                await _unitOfWork.CommitAsync();
            }

            return BadRequest("There was an error setting up your account. Please check the logs");
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
                return BadRequest(validationErrors);
            }


            if (!await ConfirmEmailToken(dto.Token, user)) return BadRequest("Invalid Email Token");

            user.UserName = dto.Username;
            var errors = await _accountService.ChangeUserPassword(user, dto.Password);
            if (errors.Any())
            {
                return BadRequest(errors);
            }
            await _unitOfWork.CommitAsync();


            user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(user.UserName,
                AppUserIncludes.UserPreferences);

            // Perform Login code
            return new UserDto
            {
                Username = user.UserName,
                Email = user.Email,
                Token = await _tokenService.CreateToken(user),
                RefreshToken = await _tokenService.CreateRefreshToken(user),
                ApiKey = user.ApiKey,
                Preferences = _mapper.Map<UserPreferencesDto>(user.UserPreferences)
            };
        }

        [AllowAnonymous]
        [HttpPost("confirm-migration-email")]
        public async Task<ActionResult<UserDto>> ConfirmMigrationEmail(ConfirmMigrationEmailDto dto)
        {
            var user = await _unitOfWork.UserRepository.GetUserByEmailAsync(dto.Email);
            if (user == null) return Unauthorized("This email is not on system");

            if (!await ConfirmEmailToken(dto.Token, user)) return BadRequest("Invalid Email Token");

            await _unitOfWork.CommitAsync();

            user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(user.UserName,
                AppUserIncludes.UserPreferences);

            // Perform Login code
            return new UserDto
            {
                Username = user.UserName,
                Email = user.Email,
                Token = await _tokenService.CreateToken(user),
                RefreshToken = await _tokenService.CreateRefreshToken(user),
                ApiKey = user.ApiKey,
                Preferences = _mapper.Map<UserPreferencesDto>(user.UserPreferences)
            };
        }

        [HttpPost("resend-confirmation-email")]
        public async Task<ActionResult<string>> ResendConfirmationSendEmail([FromQuery] int userId)
        {
            var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
            if (user == null) return BadRequest("User does not exist");

            if (string.IsNullOrEmpty(user.Email))
                return BadRequest(
                    "This user needs to migrate. Have them log out and login to trigger a migration flow");
            if (user.EmailConfirmed) return BadRequest("User already confirmed");

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var host = _environment.IsDevelopment() ? "localhost:4200" : Request.Host.ToString();
            var emailLink =
                $"{Request.Scheme}://{host}{Request.PathBase}/registration/confirm-migration-email?token={HttpUtility.UrlEncode(token)}&email={HttpUtility.UrlEncode(user.Email)}";
            _logger.LogInformation("[Email Migration]: Email Link: {Link}", emailLink);
            await _emailService.SendMigrationEmail(new EmailMigrationDto()
            {
                EmailAddress = user.Email,
                Username = user.UserName,
                ServerConfirmationLink = emailLink
            });


            return Ok(emailLink);
        }

        /// <summary>
        /// This is similar to invite. Essentially we authenticate the user's password then go through invite email flow
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpPost("migrate-email")]
        public async Task<ActionResult<string>> MigrateEmail(MigrateUserEmailDto dto)
        {
            // Check if there is an existing invite
            var emailValidationErrors = await _accountService.ValidateEmail(dto.Email);
            if (emailValidationErrors.Any())
            {
                var invitedUser = await _unitOfWork.UserRepository.GetUserByEmailAsync(dto.Email);
                if (await _userManager.IsEmailConfirmedAsync(invitedUser))
                    return BadRequest($"User is already registered as {invitedUser.UserName}");
                return BadRequest("User is already invited under this email and has yet to accepted invite.");
            }


            var user = await _userManager.Users
                .Include(u => u.UserPreferences)
                .SingleOrDefaultAsync(x => x.NormalizedUserName == dto.Username.ToUpper());
            if (user == null) return Unauthorized("Invalid username");

            var validPassword = await _signInManager.UserManager.CheckPasswordAsync(user, dto.Password);
            if (!validPassword) return Unauthorized("Your credentials are not correct");

            try
            {
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                if (string.IsNullOrEmpty(token)) return BadRequest("There was an issue sending email");
                user.Email = dto.Email;
                _unitOfWork.UserRepository.Update(user);
                await _unitOfWork.CommitAsync();

                var host = _environment.IsDevelopment() ? "localhost:4200" : Request.Host.ToString();
                var emailLink =
                    $"{Request.Scheme}://{host}{Request.PathBase}/registration/confirm-migration-email?token={HttpUtility.UrlEncode(token)}&email={HttpUtility.UrlEncode(dto.Email)}";
                _logger.LogInformation("[Email Migration]: Email Link: {Link}", emailLink);
                if (dto.SendEmail)
                {
                    await _emailService.SendMigrationEmail(new EmailMigrationDto()
                    {
                        EmailAddress = dto.Email,
                        Username = user.UserName,
                        ServerConfirmationLink = emailLink
                    });
                }
                return Ok(emailLink);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an issue during email migration. Contact support");
                _unitOfWork.UserRepository.Delete(user);
                await _unitOfWork.CommitAsync();
            }

            return BadRequest("There was an error setting up your account. Please check the logs");
        }

        private async Task<bool> ConfirmEmailToken(string token, AppUser user)
        {
            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (!result.Succeeded)
            {
                _logger.LogCritical("Email validation failed");
                if (result.Errors.Any())
                {
                    foreach (var error in result.Errors)
                    {
                        _logger.LogCritical("Email validation error: {Message}", error.Description);
                    }
                }

                return false;
            }

            return true;
        }
    }
}
