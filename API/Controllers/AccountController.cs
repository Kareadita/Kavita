using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.Account;
using API.DTOs.Email;
using API.Entities;
using API.Entities.Enums;
using API.Errors;
using API.Extensions;
using API.Helpers.Builders;
using API.Services;
using API.Services.Plus;
using API.SignalR;
using AutoMapper;
using Hangfire;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharpCompress;

namespace API.Controllers;

#nullable enable

/// <summary>
/// All Account matters
/// </summary>
public class AccountController : BaseApiController
{
    // Hardcoded to avoid localization multiple enumeration: https://github.com/Kareadita/Kavita/issues/2829
    private const string BadCredentialsMessage = "Your credentials are not correct";

    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AccountController> _logger;
    private readonly IMapper _mapper;
    private readonly IAccountService _accountService;
    private readonly IEmailService _emailService;
    private readonly IEventHub _eventHub;
    private readonly ILocalizationService _localizationService;

    /// <inheritdoc />
    public AccountController(UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        ITokenService tokenService, IUnitOfWork unitOfWork,
        ILogger<AccountController> logger,
        IMapper mapper, IAccountService accountService,
        IEmailService emailService, IEventHub eventHub,
        ILocalizationService localizationService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _mapper = mapper;
        _accountService = accountService;
        _emailService = emailService;
        _eventHub = eventHub;
        _localizationService = localizationService;
    }

    /// <summary>
    /// Update a user's password
    /// </summary>
    /// <param name="resetPasswordDto"></param>
    /// <returns></returns>
    [HttpPost("reset-password")]
    public async Task<ActionResult> UpdatePassword(ResetPasswordDto resetPasswordDto)
    {
        var user = await _userManager.Users.SingleOrDefaultAsync(x => x.UserName == resetPasswordDto.UserName);
        if (user == null) return Ok(); // Don't report BadRequest as that would allow brute forcing to find accounts on system

        _logger.LogInformation("{UserName} is changing {ResetUser}'s password", User.GetUsername(), resetPasswordDto.UserName);
        if (User.IsInRole(PolicyConstants.ReadOnlyRole))
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));
        var isAdmin = User.IsInRole(PolicyConstants.AdminRole);

        if (resetPasswordDto.UserName == User.GetUsername() && !(User.IsInRole(PolicyConstants.ChangePasswordRole) || isAdmin))
            return Unauthorized(await _localizationService.Translate(User.GetUserId(), "permission-denied"));

        if (resetPasswordDto.UserName != User.GetUsername() && !isAdmin)
            return Unauthorized(await _localizationService.Translate(User.GetUserId(), "permission-denied"));

        if (string.IsNullOrEmpty(resetPasswordDto.OldPassword) && !isAdmin)
            return BadRequest(
                new ApiException(400,
                    await _localizationService.Translate(User.GetUserId(), "password-required")));

        // If you're an admin and the username isn't yours, you don't need to validate the password
        var isResettingOtherUser = (resetPasswordDto.UserName != User.GetUsername() && isAdmin);
        if (!isResettingOtherUser && !await _userManager.CheckPasswordAsync(user, resetPasswordDto.OldPassword))
        {
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "invalid-password"));
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
        if (admins.Count > 0) return BadRequest(await _localizationService.Get("en", "denied"));

        try
        {
            var usernameValidation = await _accountService.ValidateUsername(registerDto.Username);
            if (usernameValidation.Any())
            {
                return BadRequest(usernameValidation);
            }

            var user = new AppUserBuilder(registerDto.Username, registerDto.Email,
                await _unitOfWork.SiteThemeRepository.GetDefaultTheme()).Build();

            var result = await _userManager.CreateAsync(user, registerDto.Password);
            if (!result.Succeeded) return BadRequest(result.Errors);

            // Assign default streams
            AddDefaultStreamsToUser(user);

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            if (string.IsNullOrEmpty(token)) return BadRequest(await _localizationService.Get("en", "confirm-token-gen"));
            if (!await ConfirmEmailToken(token, user)) return BadRequest(await _localizationService.Get("en",  "validate-email", token));


            var roleResult = await _userManager.AddToRoleAsync(user, PolicyConstants.AdminRole);
            if (!roleResult.Succeeded) return BadRequest(result.Errors);
            await _userManager.AddToRoleAsync(user, PolicyConstants.LoginRole);

            return new UserDto
            {
                Username = user.UserName,
                Email = user.Email,
                Token = await _tokenService.CreateToken(user),
                RefreshToken = await _tokenService.CreateRefreshToken(user),
                ApiKey = user.ApiKey,
                Preferences = _mapper.Map<UserPreferencesDto>(user.UserPreferences),
                KavitaVersion = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.InstallVersion)).Value,
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

        return BadRequest(await _localizationService.Get("en",  "register-user"));
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
        AppUser? user;
        if (!string.IsNullOrEmpty(loginDto.ApiKey))
        {
            user = await _userManager.Users
                .Include(u => u.UserPreferences)
                .AsSplitQuery()
                .SingleOrDefaultAsync(x => x.ApiKey == loginDto.ApiKey);
        }
        else
        {
            user = await _userManager.Users
                .Include(u => u.UserPreferences)
                .AsSplitQuery()
                .SingleOrDefaultAsync(x => x.NormalizedUserName == loginDto.Username.ToUpperInvariant());
        }

        _logger.LogInformation("{UserName} attempting to login from {IpAddress}", loginDto.Username, HttpContext.Connection.RemoteIpAddress?.ToString());

        if (user == null)
        {
            _logger.LogWarning("Attempted login by {UserName} failed due to unable to find account", loginDto.Username);
            return Unauthorized(BadCredentialsMessage);
        }
        var roles = await _userManager.GetRolesAsync(user);
        if (!roles.Contains(PolicyConstants.LoginRole)) return Unauthorized(await _localizationService.Translate(user.Id, "disabled-account"));

        if (string.IsNullOrEmpty(loginDto.ApiKey))
        {
            var result = await _signInManager
                .CheckPasswordSignInAsync(user, loginDto.Password, true);

            if (result.IsLockedOut)
            {
                await _userManager.UpdateSecurityStampAsync(user);
                var errorStr = await _localizationService.Translate(user.Id, "locked-out");
                _logger.LogWarning("{UserName} failed to log in at {Time}: {Issue}", user.UserName, user.LastActive,
                    errorStr);
                return Unauthorized(errorStr);
            }

            if (!result.Succeeded)
            {
                string errorStr = result.IsNotAllowed
                                ? await _localizationService.Translate(user.Id, "confirm-email")
                                : BadCredentialsMessage;
                _logger.LogWarning("{UserName} failed to log in at {Time}: {Issue}", user.UserName, user.LastActive, errorStr);
                return Unauthorized(errorStr);
            }
        }

        // Update LastActive on account
        user.UpdateLastActive();

        // NOTE: This can likely be removed
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
        dto.KavitaVersion = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.InstallVersion))
            .Value;
        var pref = await _unitOfWork.UserRepository.GetPreferencesAsync(user.UserName!);
        if (pref == null) return Ok(dto);

        pref.Theme ??= await _unitOfWork.SiteThemeRepository.GetDefaultTheme();
        dto.Preferences = _mapper.Map<UserPreferencesDto>(pref);

        return Ok(dto);
    }

    /// <summary>
    /// Returns an up-to-date user account
    /// </summary>
    /// <returns></returns>
    [HttpGet("refresh-account")]
    public async Task<ActionResult<UserDto>> RefreshAccount()
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(User.GetUserId(), AppUserIncludes.UserPreferences);
        if (user == null) return Unauthorized();

        var dto = _mapper.Map<UserDto>(user);
        dto.Token = await _tokenService.CreateToken(user);
        dto.RefreshToken = await _tokenService.CreateRefreshToken(user);
        dto.KavitaVersion = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.InstallVersion))
            .Value;
        dto.Preferences = _mapper.Map<UserPreferencesDto>(user.UserPreferences);
        return Ok(dto);
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
            return Unauthorized(new { message = await _localizationService.Get("en", "invalid-token") });
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
                f => (string) f.GetValue(null)!).Values.ToList();
    }


    /// <summary>
    /// Resets the API Key assigned with a user
    /// </summary>
    /// <remarks>This will log unauthorized requests to Security log</remarks>
    /// <returns></returns>
    [HttpPost("reset-api-key")]
    public async Task<ActionResult<string>> ResetApiKey()
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername()) ?? throw new KavitaUnauthenticatedUserException();
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));
        user.ApiKey = HashUtil.ApiKey();

        if (_unitOfWork.HasChanges() && await _unitOfWork.CommitAsync())
        {
            await _eventHub.SendMessageToAsync(MessageFactory.UserUpdate,
                MessageFactory.UserUpdateEvent(user.Id, user.UserName), user.Id);
            return Ok(user.ApiKey);
        }

        await _unitOfWork.RollbackAsync();
        return BadRequest(await _localizationService.Translate(User.GetUserId(), "unable-to-reset-key"));
    }


    /// <summary>
    /// Initiates the flow to update a user's email address.
    ///
    /// If email is not setup, then the email address is not changed in this API. A confirmation link is sent/dumped which will
    /// validate the email. It must be confirmed for the email to update.
    /// </summary>
    /// <param name="dto"></param>
    /// <returns>Returns just if the email was sent or server isn't reachable</returns>
    [HttpPost("update/email")]
    public async Task<ActionResult> UpdateEmail(UpdateEmailDto? dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
        if (user == null || User.IsInRole(PolicyConstants.ReadOnlyRole)) return Unauthorized(await _localizationService.Translate(User.GetUserId(), "permission-denied"));

        if (dto == null || string.IsNullOrEmpty(dto.Email) || string.IsNullOrEmpty(dto.Password))
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "invalid-payload"));


        // Validate this user's password
        if (! await _userManager.CheckPasswordAsync(user, dto.Password))
        {
            _logger.LogCritical("A user tried to change {UserName}'s email, but password didn't validate", user.UserName);
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));
        }

        // Validate no other users exist with this email
        if (user.Email!.Equals(dto.Email)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "nothing-to-do"));

        // Check if email is used by another user
        var existingUserEmail = await _unitOfWork.UserRepository.GetUserByEmailAsync(dto.Email);
        if (existingUserEmail != null)
        {
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "share-multiple-emails"));
        }

        // All validations complete, generate a new token and email it to the user at the new address. Confirm email link will update the email
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogError("There was an issue generating a token for the email");
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "generate-token"));
        }

        var serverSettings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        var shouldEmailUser = serverSettings.IsEmailSetup() || !_emailService.IsValidEmail(user.Email);
        user.EmailConfirmed = !shouldEmailUser;
        user.ConfirmationToken = token;
        await _userManager.UpdateAsync(user);

        var emailLink = await _emailService.GenerateEmailLink(Request, user.ConfirmationToken, "confirm-email-update", dto.Email);
        _logger.LogCritical("[Update Email]: Email Link for {UserName}: {Link}", user.UserName, emailLink);

        if (!shouldEmailUser)
        {
            _logger.LogInformation("Cannot email admin, email not setup or admin email invalid");
            return Ok(new InviteUserResponse
            {
                EmailLink = string.Empty,
                EmailSent = false
            });
        }


        // Send a confirmation email
        try
        {
            if (!_emailService.IsValidEmail(user.Email))
            {
                _logger.LogCritical("[Update Email]: User is trying to update their email, but their existing email ({Email}) isn't valid. No email will be send", user.Email);
                return Ok(new InviteUserResponse
                {
                    EmailLink = string.Empty,
                    EmailSent = false,
                    InvalidEmail = true,
                });
            }


            try
            {
                var invitingUser = (await _unitOfWork.UserRepository.GetAdminUsersAsync()).First().UserName!;
                // Email the old address of the update change
                BackgroundJob.Enqueue(() => _emailService.SendEmailChangeEmail(new ConfirmationEmailDto()
                {
                    EmailAddress = string.IsNullOrEmpty(user.Email) ? dto.Email : user.Email,
                    InstallId = BuildInfo.Version.ToString(),
                    InvitingUser = invitingUser,
                    ServerConfirmationLink = emailLink
                }));
            }
            catch (Exception)
            {
                /* Swallow exception */
            }

            return Ok(new InviteUserResponse
            {
                EmailLink = string.Empty,
                EmailSent = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an error during invite user flow, unable to send an email");
        }

        await _eventHub.SendMessageToAsync(MessageFactory.UserUpdate, MessageFactory.UserUpdateEvent(user.Id, user.UserName!), user.Id);

        return Ok();
    }

    [HttpPost("update/age-restriction")]
    public async Task<ActionResult> UpdateAgeRestriction(UpdateAgeRestrictionDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
        if (user == null) return Unauthorized(await _localizationService.Translate(User.GetUserId(), "permission-denied"));
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));

        var isAdmin = await _unitOfWork.UserRepository.IsUserAdminAsync(user);
        if (!await _accountService.CanChangeAgeRestriction(user)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));

        user.AgeRestriction = isAdmin ? AgeRating.NotApplicable : dto.AgeRating;
        user.AgeRestrictionIncludeUnknowns = isAdmin || dto.IncludeUnknowns;

        _unitOfWork.UserRepository.Update(user);

        if (!_unitOfWork.HasChanges()) return Ok();
        try
        {
            await _unitOfWork.CommitAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an error updating the age restriction");
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "age-restriction-update"));
        }

        await _eventHub.SendMessageToAsync(MessageFactory.UserUpdate, MessageFactory.UserUpdateEvent(user.Id, user.UserName!), user.Id);

        return Ok();
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
        if (adminUser == null) return Unauthorized();
        if (!await _unitOfWork.UserRepository.IsUserAdminAsync(adminUser)) return Unauthorized(await _localizationService.Translate(User.GetUserId(), "permission-denied"));
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));

        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(dto.UserId, AppUserIncludes.SideNavStreams);
        if (user == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "no-user"));

        // Check if username is changing
        if (!user.UserName!.Equals(dto.Username))
        {
            // Validate username change
            var errors = await _accountService.ValidateUsername(dto.Username);
            if (errors.Any()) return BadRequest(await _localizationService.Translate(User.GetUserId(), "username-taken"));
            user.UserName = dto.Username;
            await _userManager.UpdateNormalizedUserNameAsync(user);
            _unitOfWork.UserRepository.Update(user);
        }

        // Check if email is changing for a non-admin user
        var isUpdatingAnotherAccount = user.Id != adminUser.Id;
        if (isUpdatingAnotherAccount && !string.IsNullOrEmpty(dto.Email) && user.Email != dto.Email)
        {
            // Validate username change
            var errors = await _accountService.ValidateEmail(dto.Email);
            if (errors.Any()) return BadRequest(await _localizationService.Translate(User.GetUserId(), "email-taken"));

            user.Email = dto.Email;
            user.EmailConfirmed = true; // When an admin performs the flow, we assume the email address is able to receive data

            await _userManager.UpdateNormalizedEmailAsync(user);
            _unitOfWork.UserRepository.Update(user);
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

        // We might want to check if they had admin and no longer, if so:
        // await _userManager.UpdateSecurityStampAsync(user); to force them to re-authenticate


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
                user.RemoveSideNavFromLibrary(lib);
            }

            libraries = (await _unitOfWork.LibraryRepository.GetLibraryForIdsAsync(dto.Libraries, LibraryIncludes.AppUser)).ToList();
        }

        foreach (var lib in libraries)
        {
            lib.AppUsers ??= new List<AppUser>();
            lib.AppUsers.Add(user);
            user.CreateSideNavFromLibrary(lib);
        }

        user.AgeRestriction = hasAdminRole ? AgeRating.NotApplicable : dto.AgeRestriction.AgeRating;
        user.AgeRestrictionIncludeUnknowns = hasAdminRole || dto.AgeRestriction.IncludeUnknowns;

        _unitOfWork.UserRepository.Update(user);

        if (!_unitOfWork.HasChanges() || await _unitOfWork.CommitAsync())
        {
            await _eventHub.SendMessageToAsync(MessageFactory.UserUpdate, MessageFactory.UserUpdateEvent(user.Id, user.UserName), user.Id);
            await _eventHub.SendMessageToAsync(MessageFactory.SideNavUpdate, MessageFactory.SideNavUpdateEvent(user.Id), user.Id);
            // If we adjust library access, dashboards should re-render
            await _eventHub.SendMessageToAsync(MessageFactory.DashboardUpdate, MessageFactory.DashboardUpdateEvent(user.Id), user.Id);
            return Ok();
        }

        await _unitOfWork.RollbackAsync();
        return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-user-update"));
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
        if (user == null) return Unauthorized();
        if (user.EmailConfirmed)
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "user-already-confirmed"));
        if (string.IsNullOrEmpty(user.ConfirmationToken))
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "manual-setup-fail"));

        return await _emailService.GenerateEmailLink(Request, user.ConfirmationToken, "confirm-email", user.Email!, withBaseUrl);
    }


    /// <summary>
    /// Invites a user to the server. Will generate a setup link for continuing setup. If email is not setup, a link will be presented to user to continue setup.
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("invite")]
    public async Task<ActionResult<string>> InviteUser(InviteUserDto dto)
    {
        var userId = User.GetUserId();
        var adminUser = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
        if (adminUser == null) return Unauthorized(await _localizationService.Translate(userId, "permission-denied"));

        dto.Email = dto.Email.Trim();
        if (string.IsNullOrEmpty(dto.Email)) return BadRequest(await _localizationService.Translate(userId, "invalid-payload"));

        _logger.LogInformation("{User} is inviting {Email} to the server", adminUser.UserName, dto.Email);

        // Check if there is an existing invite
        var emailValidationErrors = await _accountService.ValidateEmail(dto.Email);
        if (emailValidationErrors.Any())
        {
            var invitedUser = await _unitOfWork.UserRepository.GetUserByEmailAsync(dto.Email);
            if (await _userManager.IsEmailConfirmedAsync(invitedUser!))
                return BadRequest(await _localizationService.Translate(User.GetUserId(), "user-already-registered", invitedUser!.UserName));
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "user-already-invited"));
        }

        // Create a new user
        var user = new AppUserBuilder(dto.Email, dto.Email,
            await _unitOfWork.SiteThemeRepository.GetDefaultTheme()).Build();
        _unitOfWork.UserRepository.Add(user);
        try
        {
            var result = await _userManager.CreateAsync(user, AccountService.DefaultPassword);
            if (!result.Succeeded) return BadRequest(result.Errors);

            // Assign default streams
            AddDefaultStreamsToUser(user);

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
                user.CreateSideNavFromLibrary(lib);
            }

            user.AgeRestriction = hasAdminRole ? AgeRating.NotApplicable : dto.AgeRestriction.AgeRating;
            user.AgeRestrictionIncludeUnknowns = hasAdminRole || dto.AgeRestriction.IncludeUnknowns;

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("There was an issue generating a token for the email");
                return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-invite-user"));
            }

            user.ConfirmationToken = token;
            _unitOfWork.UserRepository.Update(user);
            await _unitOfWork.CommitAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an error during invite user flow, unable to create user. Deleting user for retry");
            _unitOfWork.UserRepository.Delete(user);
            await _unitOfWork.CommitAsync();
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-invite-user"));
        }

        try
        {
            var emailLink = await _emailService.GenerateEmailLink(Request, user.ConfirmationToken, "confirm-email", dto.Email);
            _logger.LogCritical("[Invite User]: Email Link for {UserName}: {Link}", user.UserName, emailLink);

            var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
            if (!_emailService.IsValidEmail(dto.Email) || !settings.IsEmailSetup())
            {
                _logger.LogInformation("[Invite User] {Email} doesn't appear to be an email or email is not setup", dto.Email.Replace(Environment.NewLine, string.Empty));
                return Ok(new InviteUserResponse
                {
                    EmailLink = emailLink,
                    EmailSent = false,
                    InvalidEmail = true
                });
            }

            BackgroundJob.Enqueue(() => _emailService.SendInviteEmail(new ConfirmationEmailDto()
            {
                EmailAddress = dto.Email,
                InvitingUser = adminUser.UserName,
                ServerConfirmationLink = emailLink
            }));

            return Ok(new InviteUserResponse
            {
                EmailLink = emailLink,
                EmailSent = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an error during invite user flow, unable to send an email");
        }

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-invite-user"));
    }

    private void AddDefaultStreamsToUser(AppUser user)
    {
        foreach (var newStream in Seed.DefaultStreams.Select(stream => _mapper.Map<AppUserDashboardStream, AppUserDashboardStream>(stream)))
        {
            user.DashboardStreams.Add(newStream);
        }

        foreach (var stream in Seed.DefaultSideNavStreams.Select(stream => _mapper.Map<AppUserSideNavStream, AppUserSideNavStream>(stream)))
        {
            user.SideNavStreams.Add(stream);
        }
    }

    /// <summary>
    /// Last step in authentication flow, confirms the email token for email
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpPost("confirm-email")]
    public async Task<ActionResult<UserDto>> ConfirmEmail(ConfirmEmailDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByEmailAsync(dto.Email);

        if (user == null)
        {
            _logger.LogInformation("confirm-email failed from invalid registered email: {Email}", dto.Email);
            return BadRequest(await _localizationService.Get("en", "invalid-email-confirmation"));
        }

        // Validate Password and Username
        var validationErrors = new List<ApiException>();
        // This allows users that use a fake email with the same username to continue setting up the account
        if (!dto.Username.Equals(dto.Email) && !user.UserName!.Equals(dto.Username))
        {
            validationErrors.AddRange(await _accountService.ValidateUsername(dto.Username));
        }

        validationErrors.AddRange(await _accountService.ValidatePassword(user, dto.Password));

        if (validationErrors.Any())
        {
            return BadRequest(validationErrors);
        }


        if (!await ConfirmEmailToken(dto.Token, user))
        {
            _logger.LogInformation("confirm-email failed from invalid token: {Token}", dto.Token);
            return BadRequest(await _localizationService.Translate(user.Id, "invalid-email-confirmation"));
        }

        user.UserName = dto.Username;
        user.ConfirmationToken = null;
        var errors = await _accountService.ChangeUserPassword(user, dto.Password);
        if (errors.Any())
        {
            return BadRequest(errors);
        }
        await _unitOfWork.CommitAsync();


        user = (await _unitOfWork.UserRepository.GetUserByUsernameAsync(user.UserName,
            AppUserIncludes.UserPreferences))!;

        // Perform Login code
        return new UserDto
        {
            Username = user.UserName!,
            Email = user.Email!,
            Token = await _tokenService.CreateToken(user),
            RefreshToken = await _tokenService.CreateRefreshToken(user),
            ApiKey = user.ApiKey,
            Preferences = _mapper.Map<UserPreferencesDto>(user.UserPreferences),
            KavitaVersion = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.InstallVersion)).Value,
        };
    }

    /// <summary>
    /// Final step in email update change. Given a confirmation token and the email, this will finish the email change.
    /// </summary>
    /// <remarks>This will force connected clients to re-authenticate</remarks>
    /// <param name="dto"></param>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpPost("confirm-email-update")]
    public async Task<ActionResult> ConfirmEmailUpdate(ConfirmEmailUpdateDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByConfirmationToken(dto.Token);
        if (user == null)
        {
            _logger.LogInformation("confirm-email failed from invalid registered email: {Email}", dto.Email);
            return BadRequest(await _localizationService.Get("en", "invalid-email-confirmation"));
        }

        if (!await ConfirmEmailToken(dto.Token, user))
        {
            _logger.LogInformation("confirm-email failed from invalid token: {Token}", dto.Token);
            return BadRequest(await _localizationService.Translate(user.Id, "invalid-email-confirmation"));
        }

        _logger.LogInformation("User is updating email from {OldEmail} to {NewEmail}", user.Email, dto.Email);
        var result = await _userManager.SetEmailAsync(user, dto.Email);
        if (!result.Succeeded)
        {
            _logger.LogError("Unable to update email for users: {Errors}", result.Errors.Select(e => e.Description));
            return BadRequest(await _localizationService.Translate(user.Id, "generic-user-email-update"));
        }
        user.ConfirmationToken = null;
        user.EmailConfirmed = true;
        await _unitOfWork.CommitAsync();


        // For the user's connected devices to pull the new information in
        await _eventHub.SendMessageToAsync(MessageFactory.UserUpdate,
            MessageFactory.UserUpdateEvent(user.Id, user.UserName!), user.Id);

        return Ok();
    }

    [AllowAnonymous]
    [HttpPost("confirm-password-reset")]
    public async Task<ActionResult<string>> ConfirmForgotPassword(ConfirmPasswordResetDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByEmailAsync(dto.Email);
        if (user == null)
        {
            return BadRequest(BadCredentialsMessage);
        }

        try
        {
            var result = await _userManager.VerifyUserTokenAsync(user, TokenOptions.DefaultProvider,
                "ResetPassword", dto.Token);
            if (!result)
            {
                _logger.LogInformation("Unable to reset password, your email token is not correct: {@Dto}", dto);
                return BadRequest(BadCredentialsMessage);
            }

            var errors = await _accountService.ChangeUserPassword(user, dto.Password);
            return errors.Any() ? BadRequest(errors) : Ok(await _localizationService.Translate(user.Id, "password-updated"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an unexpected error when confirming new password");
            return BadRequest(await _localizationService.Translate(user.Id, "generic-password-update"));
        }
    }


    /// <summary>
    /// Will send user a link to update their password to their email or prompt them if not accessible
    /// </summary>
    /// <param name="email"></param>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [EnableRateLimiting("Authentication")]
    public async Task<ActionResult<string>> ForgotPassword([FromQuery] string email)
    {
        var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        var user = await _unitOfWork.UserRepository.GetUserByEmailAsync(email);
        if (user == null)
        {
            _logger.LogError("There are no users with email: {Email} but user is requesting password reset", email);
            return Ok(await _localizationService.Get("en", "forgot-password-generic"));
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (!roles.Any(r => r is PolicyConstants.AdminRole or PolicyConstants.ChangePasswordRole or PolicyConstants.ReadOnlyRole))
            return Unauthorized(await _localizationService.Translate(user.Id, "permission-denied"));

        if (string.IsNullOrEmpty(user.Email) || !user.EmailConfirmed)
            return BadRequest(await _localizationService.Translate(user.Id, "confirm-email"));



        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var emailLink = await _emailService.GenerateEmailLink(Request, token, "confirm-reset-password", user.Email);
        user.ConfirmationToken = token;
        _unitOfWork.UserRepository.Update(user);
        await _unitOfWork.CommitAsync();
        _logger.LogCritical("[Forgot Password]: Email Link for {UserName}: {Link}", user.UserName, emailLink);

        if (!settings.IsEmailSetup()) return Ok(await _localizationService.Get("en", "email-not-enabled"));
        if (!_emailService.IsValidEmail(user.Email))
        {
            _logger.LogCritical("[Forgot Password]: User is trying to do a forgot password flow, but their email ({Email}) isn't valid. No email will be send. Admin must change it in UI or from url above", user.Email);
            return Ok(await _localizationService.Translate(user.Id, "invalid-email"));
        }

        var installId = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.InstallId)).Value;
        BackgroundJob.Enqueue(() => _emailService.SendForgotPasswordEmail(new PasswordResetEmailDto()
        {
            EmailAddress = user.Email,
            ServerConfirmationLink = emailLink,
            InstallId = installId
        }));

        return Ok(await _localizationService.Translate(user.Id, "email-sent"));
    }

    [HttpGet("email-confirmed")]
    public async Task<ActionResult<bool>> IsEmailConfirmed()
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
        if (user == null) return Unauthorized();

        return Ok(user.EmailConfirmed);
    }

    [AllowAnonymous]
    [HttpPost("confirm-migration-email")]
    public async Task<ActionResult<UserDto>> ConfirmMigrationEmail(ConfirmMigrationEmailDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByEmailAsync(dto.Email);
        if (user == null) return BadRequest(BadCredentialsMessage);

        if (!await ConfirmEmailToken(dto.Token, user))
        {
            _logger.LogInformation("confirm-migration-email email token is invalid");
            return BadRequest(BadCredentialsMessage);
        }

        await _unitOfWork.CommitAsync();

        user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(user.UserName!,
            AppUserIncludes.UserPreferences);

        // Perform Login code
        return new UserDto
        {
            Username = user!.UserName!,
            Email = user.Email!,
            Token = await _tokenService.CreateToken(user),
            RefreshToken = await _tokenService.CreateRefreshToken(user),
            ApiKey = user.ApiKey,
            Preferences = _mapper.Map<UserPreferencesDto>(user.UserPreferences),
            KavitaVersion = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.InstallVersion)).Value,
        };
    }

    /// <summary>
    /// Resend an invite to a user already invited
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    [Authorize("RequireAdminRole")]
    [HttpPost("resend-confirmation-email")]
    [EnableRateLimiting("Authentication")]
    public async Task<ActionResult<InviteUserResponse>> ResendConfirmationSendEmail([FromQuery] int userId)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
        if (user == null) return BadRequest(await _localizationService.Get("en", "no-user"));

        if (string.IsNullOrEmpty(user.Email))
            return BadRequest(
                await _localizationService.Translate(user.Id, "user-migration-needed"));
        if (user.EmailConfirmed) return BadRequest(await _localizationService.Translate(user.Id, "user-already-confirmed"));

        // TODO: If the target user is read only, we might want to just forgo this

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        user.ConfirmationToken = token;
        _unitOfWork.UserRepository.Update(user);
        await _unitOfWork.CommitAsync();
        var emailLink = await _emailService.GenerateEmailLink(Request, token, "confirm-email-update", user.Email);
        _logger.LogCritical("[Email Migration]: Email Link for {UserName}: {Link}", user.UserName, emailLink);

        if (!_emailService.IsValidEmail(user.Email))
        {
            _logger.LogCritical("[Email Migration]: User {UserName} is trying to resend an invite flow, but their email ({Email}) isn't valid. No email will be send", user.UserName, user.Email);
        }


        var serverSettings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        var shouldEmailUser = serverSettings.IsEmailSetup() || !_emailService.IsValidEmail(user.Email);

        if (!shouldEmailUser)
        {
            return Ok(new InviteUserResponse()
            {
                EmailLink = emailLink,
                EmailSent = false,
                InvalidEmail = !_emailService.IsValidEmail(user.Email)
            });
        }

        BackgroundJob.Enqueue(() => _emailService.SendInviteEmail(new ConfirmationEmailDto()
        {
            EmailAddress = user.Email!,
            InvitingUser = User.GetUsername(),
            ServerConfirmationLink = emailLink,
            InstallId = serverSettings.InstallId
        }));

        return Ok(new InviteUserResponse()
        {
            EmailLink = emailLink,
            EmailSent = true,
            InvalidEmail = !_emailService.IsValidEmail(user.Email)
        });
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

    /// <summary>
    /// Returns the OPDS url for this user
    /// </summary>
    /// <returns></returns>
    [HttpGet("opds-url")]
    public async Task<ActionResult<string>> GetOpdsUrl()
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(User.GetUserId());
        var serverSettings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        var origin = HttpContext.Request.Scheme + "://" + HttpContext.Request.Host.Value;
        if (!string.IsNullOrEmpty(serverSettings.HostName)) origin = serverSettings.HostName;

        var baseUrl = string.Empty;
        if (!string.IsNullOrEmpty(serverSettings.BaseUrl) &&
            !serverSettings.BaseUrl.Equals(Configuration.DefaultBaseUrl))
        {
            baseUrl = serverSettings.BaseUrl + "/";
            if (baseUrl.EndsWith("//"))
            {
                baseUrl = baseUrl.Replace("//", "/");
            }

            if (baseUrl.StartsWith('/'))
            {
                baseUrl = baseUrl.Substring(1, baseUrl.Length - 1);
            }
        }
        return Ok(origin + "/" + baseUrl + "api/opds/" + user!.ApiKey);
    }


    /// <summary>
    /// Is the user's current email valid or not
    /// </summary>
    /// <returns></returns>
    [HttpGet("is-email-valid")]
    public async Task<ActionResult<bool>> IsEmailValid()
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(User.GetUserId());
        if (user == null) return Unauthorized();
        if (string.IsNullOrEmpty(user.Email)) return Ok(false);

        return Ok(_emailService.IsValidEmail(user.Email));
    }
}
