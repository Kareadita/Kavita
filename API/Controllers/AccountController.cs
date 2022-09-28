﻿using System;
using System.Collections.Generic;
using System.Linq;
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
using API.Entities.Enums;
using API.Entities.Enums.UserPreferences;
using API.Errors;
using API.Extensions;
using API.Services;
using API.SignalR;
using AutoMapper;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace API.Controllers;

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
    private readonly IEventHub _eventHub;

    /// <inheritdoc />
    public AccountController(UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        ITokenService tokenService, IUnitOfWork unitOfWork,
        ILogger<AccountController> logger,
        IMapper mapper, IAccountService accountService,
        IEmailService emailService, IHostEnvironment environment,
        IEventHub eventHub)
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
        _eventHub = eventHub;
    }

    /// <summary>
    /// Update a user's password
    /// </summary>
    /// <param name="resetPasswordDto"></param>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<ActionResult> UpdatePassword(ResetPasswordDto resetPasswordDto)
    {
        // TODO: Log this request to Audit Table
        _logger.LogInformation("{UserName} is changing {ResetUser}'s password", User.GetUsername(), resetPasswordDto.UserName);

        var user = await _userManager.Users.SingleOrDefaultAsync(x => x.UserName == resetPasswordDto.UserName);
        if (user == null) return Ok(); // Don't report BadRequest as that would allow brute forcing to find accounts on system
        var isAdmin = User.IsInRole(PolicyConstants.AdminRole);


        if (resetPasswordDto.UserName == User.GetUsername() && !(User.IsInRole(PolicyConstants.ChangePasswordRole) || isAdmin))
            return Unauthorized("You are not permitted to this operation.");

        if (resetPasswordDto.UserName != User.GetUsername() && !isAdmin)
            return Unauthorized("You are not permitted to this operation.");

        if (string.IsNullOrEmpty(resetPasswordDto.OldPassword) && !isAdmin)
            return BadRequest(new ApiException(400, "You must enter your existing password to change your account unless you're an admin"));

        // If you're an admin and the username isn't yours, you don't need to validate the password
        var isResettingOtherUser = (resetPasswordDto.UserName != User.GetUsername() && isAdmin);
        if (!isResettingOtherUser && !await _userManager.CheckPasswordAsync(user, resetPasswordDto.OldPassword))
        {
            return BadRequest("Invalid Password");
        }

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
    [AllowAnonymous]
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
                UserPreferences = new AppUserPreferences
                {
                    Theme = await _unitOfWork.SiteThemeRepository.GetDefaultTheme()
                },
                ApiKey = HashUtil.ApiKey()
            };

            var result = await _userManager.CreateAsync(user, registerDto.Password);
            if (!result.Succeeded) return BadRequest(result.Errors);

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            if (string.IsNullOrEmpty(token)) return BadRequest("There was an issue generating a confirmation token.");
            if (!await ConfirmEmailToken(token, user)) return BadRequest($"There was an issue validating your email: {token}");


            var roleResult = await _userManager.AddToRoleAsync(user, PolicyConstants.AdminRole);
            if (!roleResult.Succeeded) return BadRequest(result.Errors);

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
            // We need to manually delete the User as we've already committed
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(registerDto.Username);
            _unitOfWork.UserRepository.Delete(user);
            await _unitOfWork.CommitAsync();
        }

        return BadRequest("Something went wrong when registering user");
    }


    /// <summary>
    /// Perform a login. Will send JWT Token of the logged in user back.
    /// </summary>
    /// <param name="loginDto"></param>
    /// <returns></returns>
    [AllowAnonymous]
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

        var result = await _signInManager
            .CheckPasswordSignInAsync(user, loginDto.Password, true);

        if (result.IsLockedOut)
        {
            return Unauthorized("You've been locked out from too many authorization attempts. Please wait 10 minutes.");
        }

        if (!result.Succeeded)
        {
            return Unauthorized(result.IsNotAllowed ? "You must confirm your email first" : "Your credentials are not correct.");
        }

        // Update LastActive on account
        user.LastActive = DateTime.Now;
        user.UserPreferences ??= new AppUserPreferences
        {
            Theme = await _unitOfWork.SiteThemeRepository.GetDefaultTheme()
        };

        _unitOfWork.UserRepository.Update(user);
        await _unitOfWork.CommitAsync();

        _logger.LogInformation("{UserName} logged in at {Time}", user.UserName, user.LastActive);

        var dto = _mapper.Map<UserDto>(user);
        dto.Token = await _tokenService.CreateToken(user);
        dto.RefreshToken = await _tokenService.CreateRefreshToken(user);
        var pref = await _unitOfWork.UserRepository.GetPreferencesAsync(user.UserName);
        pref.Theme ??= await _unitOfWork.SiteThemeRepository.GetDefaultTheme();
        dto.Preferences = _mapper.Map<UserPreferencesDto>(pref);
        return dto;
    }

    /// <summary>
    /// Refreshes the user's JWT token
    /// </summary>
    /// <param name="tokenRequestDto"></param>
    /// <returns></returns>
    [AllowAnonymous]
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
        // TODO: This should be moved to ServerController
        return typeof(PolicyConstants)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .ToDictionary(f => f.Name,
                f => (string) f.GetValue(null)).Values.ToList();
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

        if (existingRoles.Except(dto.Roles).Any() || dto.Roles.Except(existingRoles).Any())
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

            libraries = (await _unitOfWork.LibraryRepository.GetLibraryForIdsAsync(dto.Libraries, LibraryIncludes.AppUser)).ToList();
        }

        foreach (var lib in libraries)
        {
            lib.AppUsers ??= new List<AppUser>();
            lib.AppUsers.Add(user);
        }

        if (!_unitOfWork.HasChanges() || await _unitOfWork.CommitAsync())
        {
            await _eventHub.SendMessageToAsync(MessageFactory.UserUpdate, MessageFactory.UserUpdateEvent(user.Id, user.UserName), user.Id);
            return Ok();
        }

        await _unitOfWork.RollbackAsync();
        return BadRequest("There was an exception when updating the user");
    }

    /// <summary>
    /// Requests the Invite Url for the UserId. Will return error if user is already validated.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="withBaseUrl">Include the "https://ip:port/" in the generated link</param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpGet("invite-url")]
    public async Task<ActionResult<string>> GetInviteUrl(int userId, bool withBaseUrl)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
        if (user.EmailConfirmed)
            return BadRequest("User is already confirmed");
        if (string.IsNullOrEmpty(user.ConfirmationToken))
            return BadRequest("Manual setup is unable to be completed. Please cancel and recreate the invite.");

        return GenerateEmailLink(user.ConfirmationToken, "confirm-email", user.Email, withBaseUrl);
    }


    /// <summary>
    /// Invites a user to the server. Will generate a setup link for continuing setup. If the server is not accessible, no
    /// email will be sent.
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("invite")]
    public async Task<ActionResult<string>> InviteUser(InviteUserDto dto)
    {
        var adminUser = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
        if (adminUser == null) return Unauthorized("You need to login");
        _logger.LogInformation("{User} is inviting {Email} to the server", adminUser.UserName, dto.Email);

        // Check if there is an existing invite
        dto.Email = dto.Email.Trim();
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
            UserPreferences = new AppUserPreferences
            {
                Theme = await _unitOfWork.SiteThemeRepository.GetDefaultTheme()
            }
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
                        BadRequest(roleResult.Errors);
            }

            // Grant access to libraries
            List<Library> libraries;
            if (hasAdminRole)
            {
                _logger.LogInformation("{UserName} is being registered as admin. Granting access to all libraries",
                    user.UserName);
                libraries = (await _unitOfWork.LibraryRepository.GetLibrariesAsync(LibraryIncludes.AppUser)).ToList();
            }
            else
            {
                libraries = (await _unitOfWork.LibraryRepository.GetLibraryForIdsAsync(dto.Libraries, LibraryIncludes.AppUser)).ToList();
            }

            foreach (var lib in libraries)
            {
                lib.AppUsers ??= new List<AppUser>();
                lib.AppUsers.Add(user);
            }

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("There was an issue generating a token for the email");
                return BadRequest("There was an creating the invite user");
            }

            user.ConfirmationToken = token;
            await _unitOfWork.CommitAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an error during invite user flow, unable to create user. Deleting user for retry");
            _unitOfWork.UserRepository.Delete(user);
            await _unitOfWork.CommitAsync();
        }

        try
        {
            var emailLink = GenerateEmailLink(user.ConfirmationToken, "confirm-email", dto.Email);
            _logger.LogCritical("[Invite User]: Email Link for {UserName}: {Link}", user.UserName, emailLink);
            _logger.LogCritical("[Invite User]: Token {UserName}: {Token}", user.UserName, user.ConfirmationToken);
            var host = _environment.IsDevelopment() ? "localhost:4200" : Request.Host.ToString();
            var accessible = await _emailService.CheckIfAccessible(host);
            if (accessible)
            {
                try
                {
                    await _emailService.SendConfirmationEmail(new ConfirmationEmailDto()
                    {
                        EmailAddress = dto.Email,
                        InvitingUser = adminUser.UserName,
                        ServerConfirmationLink = emailLink
                    });
                }
                catch (Exception)
                {
                    /* Swallow exception */
                }
            }

            return Ok(new InviteUserResponse
            {
                EmailLink = emailLink,
                EmailSent = accessible
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an error during invite user flow, unable to send an email");
        }

        return BadRequest("There was an error setting up your account. Please check the logs");
    }

    [AllowAnonymous]
    [HttpPost("confirm-email")]
    public async Task<ActionResult<UserDto>> ConfirmEmail(ConfirmEmailDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByEmailAsync(dto.Email);

        if (user == null)
        {
            return BadRequest("The email does not match the registered email");
        }

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
        user.ConfirmationToken = null;
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
    [HttpPost("confirm-password-reset")]
    public async Task<ActionResult<string>> ConfirmForgotPassword(ConfirmPasswordResetDto dto)
    {
        try
        {
            var user = await _unitOfWork.UserRepository.GetUserByEmailAsync(dto.Email);
            if (user == null)
            {
                return BadRequest("Invalid Details");
            }

            var result = await _userManager.VerifyUserTokenAsync(user, TokenOptions.DefaultProvider,
                "ResetPassword", dto.Token);
            if (!result) return BadRequest("Unable to reset password, your email token is not correct.");

            var errors = await _accountService.ChangeUserPassword(user, dto.Password);
            return errors.Any() ? BadRequest(errors) : Ok("Password updated");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an unexpected error when confirming new password");
            return BadRequest("There was an unexpected error when confirming new password");
        }
    }


    /// <summary>
    /// Will send user a link to update their password to their email or prompt them if not accessible
    /// </summary>
    /// <param name="email"></param>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<ActionResult<string>> ForgotPassword([FromQuery] string email)
    {
        var user = await _unitOfWork.UserRepository.GetUserByEmailAsync(email);
        if (user == null)
        {
            _logger.LogError("There are no users with email: {Email} but user is requesting password reset", email);
            return Ok("An email will be sent to the email if it exists in our database");
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (!roles.Any(r => r is PolicyConstants.AdminRole or PolicyConstants.ChangePasswordRole))
            return Unauthorized("You are not permitted to this operation.");

        if (string.IsNullOrEmpty(user.Email) || !user.EmailConfirmed)
            return BadRequest("You do not have an email on account or it has not been confirmed");

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var emailLink = GenerateEmailLink(token, "confirm-reset-password", user.Email);
        _logger.LogCritical("[Forgot Password]: Email Link for {UserName}: {Link}", user.UserName, emailLink);
        var host = _environment.IsDevelopment() ? "localhost:4200" : Request.Host.ToString();
        if (await _emailService.CheckIfAccessible(host))
        {
            await _emailService.SendPasswordResetEmail(new PasswordResetEmailDto()
            {
                EmailAddress = user.Email,
                ServerConfirmationLink = emailLink,
                InstallId = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.InstallId)).Value
            });
            return Ok("Email sent");
        }

        return Ok("Your server is not accessible. The Link to reset your password is in the logs.");
    }

    [AllowAnonymous]
    [HttpPost("confirm-migration-email")]
    public async Task<ActionResult<UserDto>> ConfirmMigrationEmail(ConfirmMigrationEmailDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByEmailAsync(dto.Email);
        if (user == null) return BadRequest("This email is not on system");

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
        var emailLink = GenerateEmailLink(token, "confirm-email", user.Email);
        _logger.LogCritical("[Email Migration]: Email Link: {Link}", emailLink);
        _logger.LogCritical("[Email Migration]: Token {UserName}: {Token}", user.UserName, token);
        await _emailService.SendMigrationEmail(new EmailMigrationDto()
        {
            EmailAddress = user.Email,
            Username = user.UserName,
            ServerConfirmationLink = emailLink,
            InstallId = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.InstallId)).Value
        });


        return Ok(emailLink);
    }

    private string GenerateEmailLink(string token, string routePart, string email, bool withHost = true)
    {
        var host = _environment.IsDevelopment() ? "localhost:4200" : Request.Host.ToString();
        if (withHost) return $"{Request.Scheme}://{host}{Request.PathBase}/registration/{routePart}?token={HttpUtility.UrlEncode(token)}&email={HttpUtility.UrlEncode(email)}";
        return $"registration/{routePart}?token={HttpUtility.UrlEncode(token)}&email={HttpUtility.UrlEncode(email)}";
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

            _logger.LogInformation("A user is attempting to login, but hasn't accepted email invite");
            return BadRequest("User is already invited under this email and has yet to accepted invite.");
        }


        var user = await _userManager.Users
            .Include(u => u.UserPreferences)
            .SingleOrDefaultAsync(x => x.NormalizedUserName == dto.Username.ToUpper());
        if (user == null) return BadRequest("Invalid username");

        var validPassword = await _signInManager.UserManager.CheckPasswordAsync(user, dto.Password);
        if (!validPassword) return BadRequest("Your credentials are not correct");

        try
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

            user.Email = dto.Email;
            if (!await ConfirmEmailToken(token, user)) return BadRequest("There was a critical error during migration");
            _unitOfWork.UserRepository.Update(user);

            await _unitOfWork.CommitAsync();

            return Ok();
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
        if (result.Succeeded) return true;



        _logger.LogCritical("[Account] Email validation failed");
        if (!result.Errors.Any()) return false;

        foreach (var error in result.Errors)
        {
            _logger.LogCritical("[Account] Email validation error: {Message}", error.Description);
        }

        return false;

    }
}
