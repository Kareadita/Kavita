using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AutoMapper;
using Kavita.API.Database;
using Kavita.API.Errors;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.Common;
using Kavita.Models;
using Kavita.Models.Builders;
using Kavita.Models.Constants;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;
using Kavita.Models.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Services;

public partial class AccountService(
    UserManager<AppUser> userManager,
    ILogger<AccountService> logger,
    IUnitOfWork unitOfWork,
    IMapper mapper,
    ILocalizationService localizationService)
    : IAccountService
{
    public const string DefaultPassword = "[k.2@RZ!mxCQkJzE";
    private static readonly Regex AllowedUsernameRegex = AllowedUsernameRegexAttr();


    public async Task<IEnumerable<ApiException>> ChangeUserPassword(AppUser user, string newPassword)
    {
        var passwordValidationIssues = (await ValidatePassword(user, newPassword)).ToList();
        if (passwordValidationIssues.Count != 0) return passwordValidationIssues;

        var result = await userManager.RemovePasswordAsync(user);
        if (!result.Succeeded)
        {
            logger.LogError("Could not update password");
            return result.Errors.Select(e => new ApiException(400, e.Code, e.Description));
        }

        result = await userManager.AddPasswordAsync(user, newPassword);
        if (result.Succeeded) return [];

        logger.LogError("Could not update password");
        return result.Errors.Select(e => new ApiException(400, e.Code, e.Description));
    }

    public async Task<IEnumerable<ApiException>> ValidatePassword(AppUser user, string password)
    {
        foreach (var validator in userManager.PasswordValidators)
        {
            var validationResult = await validator.ValidateAsync(userManager, user, password);
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
        if (await userManager.Users.AnyAsync(x => x.NormalizedUserName != null
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
        var user = await unitOfWork.UserRepository.GetUserByEmailAsync(email);
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
        var roles = await userManager.GetRolesAsync(user);

        return roles.Contains(PolicyConstants.BookmarkRole) || roles.Contains(PolicyConstants.AdminRole);
    }

    /// <summary>
    /// Does the user have Change Restriction permission or admin rights and not Read Only
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    public async Task<bool> CanChangeAgeRestriction(AppUser? user)
    {
        if (user == null) return false;

        var roles = await userManager.GetRolesAsync(user);
        if (roles.Contains(PolicyConstants.ReadOnlyRole)) return false;

        return roles.Contains(PolicyConstants.ChangeRestrictionRole) || roles.Contains(PolicyConstants.AdminRole);
    }

    public async Task<bool> ChangeIdentityProvider(int actingUserId, AppUser user, IdentityProvider identityProvider)
    {
        var defaultAdminUser = await unitOfWork.UserRepository.GetDefaultAdminUser();
        if (user.Id == defaultAdminUser.Id)
        {
            if (identityProvider == IdentityProvider.OpenIdConnect)
            {
                throw new KavitaException(await localizationService.Translate(actingUserId, "cannot-change-identity-provider-original-user"));
            }

            return false;
        }

        // Allow changes if users aren't being synced
        var oidcSettings = (await unitOfWork.SettingsRepository.GetSettingsDtoAsync()).OidcConfig;
        if (!oidcSettings.SyncUserSettings)
        {
            user.IdentityProvider = identityProvider;
            await unitOfWork.CommitAsync();
            return false;
        }

        // Don't allow changes to the user if they're managed by oidc, and their identity provider isn't being changed to something else
        if (user.IdentityProvider == IdentityProvider.OpenIdConnect && identityProvider == IdentityProvider.OpenIdConnect)
        {
            throw new KavitaException(await localizationService.Translate(actingUserId, "oidc-managed"));
        }

        user.IdentityProvider = identityProvider;
        await unitOfWork.CommitAsync();
        return user.IdentityProvider == IdentityProvider.OpenIdConnect;
    }

    public async Task UpdateLibrariesForUser(AppUser user, IList<int> librariesIds, bool hasAdminRole)
    {
        var allLibraries = (await unitOfWork.LibraryRepository.GetLibrariesAsync(LibraryIncludes.AppUser)).ToList();
        var currentLibrary = allLibraries.Where(l => l.AppUsers.Contains(user)).ToList();

        List<Library> libraries;
        if (hasAdminRole)
        {
            logger.LogDebug("{UserId} is admin. Granting access to all libraries", user.Id);
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
        var existingRoles = await userManager.GetRolesAsync(user);
        var hasAdminRole = roles.Contains(PolicyConstants.AdminRole);
        if (!hasAdminRole)
        {
            roles.Add(PolicyConstants.PlebRole);
        }

        if (existingRoles.Except(roles).Any() || roles.Except(existingRoles).Any())
        {
            var roleResult = await userManager.RemoveFromRolesAsync(user, existingRoles);
            if (!roleResult.Succeeded) return roleResult.Errors;

            roleResult = await userManager.AddToRolesAsync(user, roles);
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
        foreach (var newStream in Defaults.DefaultStreams.Select(mapper.Map<AppUserDashboardStream, AppUserDashboardStream>))
        {
            user.DashboardStreams.Add(newStream);
        }

        foreach (var stream in Defaults.DefaultSideNavStreams.Select(mapper.Map<AppUserSideNavStream, AppUserSideNavStream>))
        {
            user.SideNavStreams.Add(stream);
        }
    }

    private void AddDefaultHighlightSlotsToUser(AppUser user)
    {
        if (user.UserPreferences.BookReaderHighlightSlots.Any()) return;

        user.UserPreferences.BookReaderHighlightSlots = Defaults.DefaultHighlightSlots.ToList();
        unitOfWork.UserRepository.Update(user);
    }

    private void AddAuthKeys(AppUser user)
    {
        if (user.AuthKeys.Any()) return;

        user.AuthKeys = Defaults.CreateDefaultAuthKeys();
        unitOfWork.UserRepository.Update(user);
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
        unitOfWork.AppUserReadingProfileRepository.Add(profile);
        await unitOfWork.CommitAsync();
    }

    [GeneratedRegex(@"^[a-zA-Z0-9\-._@+/]*$")]
    private static partial Regex AllowedUsernameRegexAttr();
}
