using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Data.Repositories;
using API.Entities;
using API.Entities.Enums;
using API.Errors;
using API.Extensions;
using API.Helpers.Builders;
using AutoMapper;
using Kavita.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Services;

#nullable enable

public interface IAccountService
{
    Task<IEnumerable<ApiException>> ChangeUserPassword(AppUser user, string newPassword);
    Task<IEnumerable<ApiException>> ValidatePassword(AppUser user, string password);
    Task<IEnumerable<ApiException>> ValidateUsername(string? username);
    Task<IEnumerable<ApiException>> ValidateEmail(string email);
    Task<bool> HasBookmarkPermission(AppUser? user);
    Task<bool> HasDownloadPermission(AppUser? user);
    Task<bool> CanChangeAgeRestriction(AppUser? user);

    /// <summary>
    ///
    /// </summary>
    /// <param name="actingUserId">The user who is changing the identity</param>
    /// <param name="user">the user being changed</param>
    /// <param name="identityProvider"> the provider being changed to</param>
    /// <returns>If true, user should not be updated by kavita (anymore)</returns>
    /// <exception cref="KavitaException">Throws if invalid actions are being performed</exception>
    Task<bool> ChangeIdentityProvider(int actingUserId, AppUser user, IdentityProvider identityProvider);
    /// <summary>
    /// Removes access to all libraries, then grant access to all given libraries or all libraries if the user is admin.
    /// Creates side nav streams as well
    /// </summary>
    /// <param name="user"></param>
    /// <param name="librariesIds"></param>
    /// <param name="hasAdminRole"></param>
    /// <returns></returns>
    /// <remarks>Ensure that the users SideNavStreams are loaded</remarks>
    /// <remarks>Does NOT commit</remarks>
    Task UpdateLibrariesForUser(AppUser user, IList<int> librariesIds, bool hasAdminRole);
    Task<IEnumerable<IdentityError>> UpdateRolesForUser(AppUser user, IList<string> roles);
    /// <summary>
    /// Seeds all information necessary for a new user
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    Task SeedUser(AppUser user);
    void AddDefaultStreamsToUser(AppUser user);
    Task AddDefaultReadingProfileToUser(AppUser user);
}

public partial class AccountService : IAccountService
{
    private readonly ILocalizationService _localizationService;
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<AccountService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    public const string DefaultPassword = "[k.2@RZ!mxCQkJzE";
    public static readonly Regex AllowedUsernameRegex = AllowedUsernameRegexAttr();


    public AccountService(UserManager<AppUser> userManager, ILogger<AccountService> logger, IUnitOfWork unitOfWork,
        IMapper mapper, ILocalizationService localizationService)
    {
        _localizationService = localizationService;
        _userManager = userManager;
        _logger = logger;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IEnumerable<ApiException>> ChangeUserPassword(AppUser user, string newPassword)
    {
        var passwordValidationIssues = (await ValidatePassword(user, newPassword)).ToList();
        if (passwordValidationIssues.Count != 0) return passwordValidationIssues;

        var result = await _userManager.RemovePasswordAsync(user);
        if (!result.Succeeded)
        {
            _logger.LogError("Could not update password");
            return result.Errors.Select(e => new ApiException(400, e.Code, e.Description));
        }

        result = await _userManager.AddPasswordAsync(user, newPassword);
        if (result.Succeeded) return [];

        _logger.LogError("Could not update password");
        return result.Errors.Select(e => new ApiException(400, e.Code, e.Description));
    }

    public async Task<IEnumerable<ApiException>> ValidatePassword(AppUser user, string password)
    {
        foreach (var validator in _userManager.PasswordValidators)
        {
            var validationResult = await validator.ValidateAsync(_userManager, user, password);
            if (!validationResult.Succeeded)
            {
                return validationResult.Errors.Select(e => new ApiException(400, e.Code, e.Description));
            }
        }

        return Array.Empty<ApiException>();
    }
    public async Task<IEnumerable<ApiException>> ValidateUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username) || !AllowedUsernameRegex.IsMatch(username))
        {
            return [new ApiException(400, "Invalid username")];
        }

        // Reverted because of https://go.microsoft.com/fwlink/?linkid=2129535
        if (await _userManager.Users.AnyAsync(x => x.NormalizedUserName != null
                                                   && x.NormalizedUserName == username.ToUpper()))
        {
            return
            [
                new(400, "Username is already taken")
            ];
        }

        return [];
    }

    public async Task<IEnumerable<ApiException>> ValidateEmail(string email)
    {
        var user = await _unitOfWork.UserRepository.GetUserByEmailAsync(email);
        if (user == null) return [];

        return
        [
            new ApiException(400, "Email is already registered")
        ];
    }

    /// <summary>
    /// Does the user have the Bookmark permission or admin rights
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    public async Task<bool> HasBookmarkPermission(AppUser? user)
    {
        if (user == null) return false;
        var roles = await _userManager.GetRolesAsync(user);

        return roles.Contains(PolicyConstants.BookmarkRole) || roles.Contains(PolicyConstants.AdminRole);
    }

    /// <summary>
    /// Does the user have the Download permission or admin rights
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    public async Task<bool> HasDownloadPermission(AppUser? user)
    {
        if (user == null) return false;
        var roles = await _userManager.GetRolesAsync(user);

        return roles.Contains(PolicyConstants.DownloadRole) || roles.Contains(PolicyConstants.AdminRole);
    }

    /// <summary>
    /// Does the user have Change Restriction permission or admin rights and not Read Only
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    public async Task<bool> CanChangeAgeRestriction(AppUser? user)
    {
        if (user == null) return false;

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains(PolicyConstants.ReadOnlyRole)) return false;

        return roles.Contains(PolicyConstants.ChangePasswordRole) || roles.Contains(PolicyConstants.AdminRole);
    }

    public async Task<bool> ChangeIdentityProvider(int actingUserId, AppUser user, IdentityProvider identityProvider)
    {
        var defaultAdminUser = await _unitOfWork.UserRepository.GetDefaultAdminUser();
        if (user.Id == defaultAdminUser.Id)
        {
            throw new KavitaException(await _localizationService.Translate(actingUserId, "cannot-change-identity-provider-original-user"));
        }

        // Allow changes if users aren't being synced
        var oidcSettings = (await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).OidcConfig;
        if (!oidcSettings.SyncUserSettings)
        {
            user.IdentityProvider = identityProvider;
            await _unitOfWork.CommitAsync();
            return false;
        }

        // Don't allow changes to the user if they're managed by oidc, and their identity provider isn't being changed to something else
        if (user.IdentityProvider == IdentityProvider.OpenIdConnect && identityProvider == IdentityProvider.OpenIdConnect)
        {
            throw new KavitaException(await _localizationService.Translate(actingUserId, "oidc-managed"));
        }

        user.IdentityProvider = identityProvider;
        await _unitOfWork.CommitAsync();
        return user.IdentityProvider == IdentityProvider.OpenIdConnect;
    }

    public async Task UpdateLibrariesForUser(AppUser user, IList<int> librariesIds, bool hasAdminRole)
    {
        var allLibraries = (await _unitOfWork.LibraryRepository.GetLibrariesAsync(LibraryIncludes.AppUser)).ToList();
        var currentLibrary = allLibraries.Where(l => l.AppUsers.Contains(user)).ToList();

        List<Library> libraries;
        if (hasAdminRole)
        {
            _logger.LogDebug("{UserId} is admin. Granting access to all libraries", user.Id);
            libraries = allLibraries;
        }
        else
        {
            libraries = allLibraries.Where(lib => librariesIds.Contains(lib.Id)).ToList();
        }

        var toRemove = currentLibrary.Except(libraries);
        var toAdd = libraries.Except(currentLibrary);

        foreach (var lib in toRemove)
        {
            lib.AppUsers ??= [];
            lib.AppUsers.Remove(user);
            user.RemoveSideNavFromLibrary(lib);
        }

        foreach (var lib in toAdd)
        {
            lib.AppUsers ??= [];
            lib.AppUsers.Add(user);
            user.CreateSideNavFromLibrary(lib);
        }
    }

    public async Task<IEnumerable<IdentityError>> UpdateRolesForUser(AppUser user, IList<string> roles)
    {
        var existingRoles = await _userManager.GetRolesAsync(user);
        var hasAdminRole = roles.Contains(PolicyConstants.AdminRole);
        if (!hasAdminRole)
        {
            roles.Add(PolicyConstants.PlebRole);
        }

        if (existingRoles.Except(roles).Any() || roles.Except(existingRoles).Any())
        {
            var roleResult = await _userManager.RemoveFromRolesAsync(user, existingRoles);
            if (!roleResult.Succeeded) return roleResult.Errors;

            roleResult = await _userManager.AddToRolesAsync(user, roles);
            if (!roleResult.Succeeded) return roleResult.Errors;
        }

        return [];
    }

    public async Task SeedUser(AppUser user)
    {
        AddDefaultStreamsToUser(user);
        AddDefaultHighlightSlotsToUser(user);
        AddAuthKeys(user);
        await AddDefaultReadingProfileToUser(user); // Commits
    }

    /// <summary>
    /// Assign default streams
    /// </summary>
    /// <param name="user"></param>
    public void AddDefaultStreamsToUser(AppUser user)
    {
        foreach (var newStream in Seed.DefaultStreams.Select(_mapper.Map<AppUserDashboardStream, AppUserDashboardStream>))
        {
            user.DashboardStreams.Add(newStream);
        }

        foreach (var stream in Seed.DefaultSideNavStreams.Select(_mapper.Map<AppUserSideNavStream, AppUserSideNavStream>))
        {
            user.SideNavStreams.Add(stream);
        }
    }

    private void AddDefaultHighlightSlotsToUser(AppUser user)
    {
        if (user.UserPreferences.BookReaderHighlightSlots.Any()) return;

        user.UserPreferences.BookReaderHighlightSlots = Seed.DefaultHighlightSlots.ToList();
        _unitOfWork.UserRepository.Update(user);
    }

    private void AddAuthKeys(AppUser user)
    {
        if (user.AuthKeys.Any()) return;

        user.AuthKeys = Seed.CreateDefaultAuthKeys();
        _unitOfWork.UserRepository.Update(user);
    }

    /// <summary>
    /// Assign default reading profile
    /// </summary>
    /// <param name="user"></param>
    public async Task AddDefaultReadingProfileToUser(AppUser user)
    {
        var profile = new AppUserReadingProfileBuilder(user.Id)
            .WithName("Default Profile")
            .WithKind(ReadingProfileKind.Default)
            .Build();
        _unitOfWork.AppUserReadingProfileRepository.Add(profile);
        await _unitOfWork.CommitAsync();
    }

    [GeneratedRegex(@"^[a-zA-Z0-9\-._@+/]*$")]
    private static partial Regex AllowedUsernameRegexAttr();
}
