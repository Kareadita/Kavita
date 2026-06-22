using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
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
using Kavita.Models.DTOs.KavitaPlus.Scrobble;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;
using Kavita.Models.Extensions;
using Kavita.Services.Plus;
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


    public async Task<IEnumerable<ApiException>> ChangeUserPassword(AppUser user, string newPassword, CancellationToken ct = default)
    {
        var passwordValidationIssues = (await ValidatePassword(user, newPassword, ct)).ToList();
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

    public async Task<IEnumerable<ApiException>> ValidatePassword(AppUser user, string password, CancellationToken ct = default)
    {
        foreach (var validator in userManager.PasswordValidators)
        {
            var validationResult = await validator.ValidateAsync(userManager, user, password);
            if (!validationResult.Succeeded)
            {
                return validationResult.Errors.Select(e => new ApiException(400, e.Code, e.Description));
            }
        }

        return [];
    }
    public async Task<IEnumerable<ApiException>> ValidateUsername(string? username, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username) || !AllowedUsernameRegex.IsMatch(username))
        {
            return [new ApiException(400, "invalid-username")];
        }

        // Reverted because of https://go.microsoft.com/fwlink/?linkid=2129535
        if (await userManager.Users.AnyAsync(x => x.NormalizedUserName != null
                                                   && x.NormalizedUserName == username.ToUpper(), ct))
        {
            return
            [
                new(400, "username-taken")
            ];
        }

        return [];
    }

    public async Task<IEnumerable<ApiException>> ValidateEmail(string email, CancellationToken ct = default)
    {
        var user = await unitOfWork.UserRepository.GetUserByEmailAsync(email, ct: ct);
        if (user == null) return [];

        return
        [
            new ApiException(400, "Email is already registered")
        ];
    }

    /// <summary>
    /// Does the user have Change Restriction permission or admin rights and not Read Only
    /// </summary>
    /// <param name="user"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<bool> CanChangeAgeRestriction(AppUser? user, CancellationToken ct = default)
    {
        if (user == null) return false;

        var roles = await userManager.GetRolesAsync(user);
        if (roles.Contains(PolicyConstants.ReadOnlyRole)) return false;

        return roles.Contains(PolicyConstants.ChangeRestrictionRole) || roles.Contains(PolicyConstants.AdminRole);
    }

    public async Task<bool> ChangeIdentityProvider(int actingUserId, AppUser user, IdentityProvider identityProvider,
        CancellationToken ct = default)
    {
        var defaultAdminUser = await unitOfWork.UserRepository.GetDefaultAdminUser(ct: ct);
        if (user.Id == defaultAdminUser.Id)
        {
            if (identityProvider == IdentityProvider.OpenIdConnect)
            {
                throw new KavitaException(await localizationService.TranslateAsync(actingUserId, "cannot-change-identity-provider-original-user"));
            }

            return false;
        }

        // Allow changes if users aren't being synced
        var oidcSettings = (await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct)).OidcConfig;
        if (!oidcSettings.SyncUserSettings)
        {
            user.IdentityProvider = identityProvider;
            await unitOfWork.CommitAsync(ct);
            return false;
        }

        // Don't allow changes to the user if they're managed by oidc, and their identity provider isn't being changed to something else
        if (user.IdentityProvider == IdentityProvider.OpenIdConnect && identityProvider == IdentityProvider.OpenIdConnect)
        {
            throw new KavitaException(await localizationService.TranslateAsync(actingUserId, "oidc-managed"));
        }

        user.IdentityProvider = identityProvider;
        await unitOfWork.CommitAsync(ct);

        return user.IdentityProvider == IdentityProvider.OpenIdConnect;
    }

    public async Task UpdateLibrariesForUser(AppUser user, IList<int> librariesIds, bool hasAdminRole, CancellationToken ct = default)
    {
        var allLibraries = (await unitOfWork.LibraryRepository.GetLibrariesAsync(LibraryIncludes.AppUser, ct: ct)).ToList();
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

    public async Task<IEnumerable<IdentityError>> UpdateRolesForUser(AppUser user, IList<string> roles,
        CancellationToken ct = default)
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

    public async Task SeedUser(AppUser user, CancellationToken ct = default)
    {
        AddDefaultStreamsToUser(user, ct);
        AddDefaultHighlightSlotsToUser(user);
        AddAuthKeys(user);
        AddScrobbleProvidersToUser(user);
        await AddDefaultReadingProfileToUser(user, ct); // Commits
    }

    /// <summary>
    /// Assign default streams
    /// </summary>
    /// <param name="user"></param>
    /// <param name="ct"></param>
    public void AddDefaultStreamsToUser(AppUser user, CancellationToken ct = default)
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
    /// <param name="ct"></param>
    public async Task AddDefaultReadingProfileToUser(AppUser user, CancellationToken ct = default)
    {
        var profile = new AppUserReadingProfileBuilder(user.Id)
            .WithName("Default Profile")
            .WithKind(ReadingProfileKind.Default)
            .Build();

        unitOfWork.AppUserReadingProfileRepository.Add(profile);

        await unitOfWork.CommitAsync(ct);
    }

    public static void AddScrobbleProvidersToUser(AppUser user)
    {
        foreach (var provider in ScrobblingService.AllScrobbleProviders)
        {
            user.ScrobbleProviders[provider] = new AppUserScrobbleProvider
            {
                Provider = provider,
                Settings = new ScrobbleProviderSettingsDto()
                {

                    ProgressScrobbling = true,
                    RatingScrobbling = true,
                    WantToReadSync = true,
                    AllLibraries = true
                }
            };
        }
    }

    [GeneratedRegex(@"^[a-zA-Z0-9\-._@+/]*$")]
    private static partial Regex AllowedUsernameRegexAttr();
}
