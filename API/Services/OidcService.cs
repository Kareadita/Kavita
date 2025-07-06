#nullable enable
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Settings;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers.Builders;
using Kavita.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace API.Services;

public interface IOidcService
{
    /// <summary>
    /// Returns the user authenticated with OpenID Connect
    /// </summary>
    /// <param name="principal"></param>
    /// <returns></returns>
    /// <exception cref="KavitaException">if any requirements aren't met</exception>
    Task<AppUser?> LoginOrCreate(ClaimsPrincipal principal);
    /// <summary>
    /// Updates roles, library access and age rating. Will not modify the default admin
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="claimsPrincipal"></param>
    /// <param name="user"></param>
    Task SyncUserSettings(OidcConfigDto settings, ClaimsPrincipal claimsPrincipal, AppUser user);
    /// <summary>
    /// Remove <see cref="AppUser.ExternalId"/> from all users
    /// </summary>
    /// <returns></returns>
    Task ClearOidcIds();
}

public class OidcService(ILogger<OidcService> logger, UserManager<AppUser> userManager,
    IUnitOfWork unitOfWork, IAccountService accountService): IOidcService
{
    private const string LibraryAccessPrefix = "library-";
    private const string AgeRatingPrefix = "age-rating-";
    private const string IncludeUnknowns = AgeRatingPrefix + "include-unknowns";

    public async Task<AppUser?> LoginOrCreate(ClaimsPrincipal principal)
    {
        var settings = (await unitOfWork.SettingsRepository.GetSettingsDtoAsync()).OidcConfig;

        var externalId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(externalId))
            throw new KavitaException("errors.oidc.missing-external-id");

        var user = await unitOfWork.UserRepository.GetByExternalId(externalId, AppUserIncludes.UserPreferences);
        if (user != null) return user;

        var email = principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email))
            throw new KavitaException("errors.oidc.missing-email");

        if (settings.RequireVerifiedEmail && !principal.HasVerifiedEmail())
            throw new KavitaException("errors.oidc.email-not-verified");


        user = await unitOfWork.UserRepository.GetUserByEmailAsync(email, AppUserIncludes.UserPreferences | AppUserIncludes.SideNavStreams);
        if (user != null)
        {
            logger.LogInformation("User {UserName} has matched on email to {ExternalId}", user.Id, externalId);
            user.ExternalId = externalId;
            await unitOfWork.CommitAsync();
            return user;
        }

        // Cannot match on native account, try and create new one
        if (settings.SyncUserSettings && principal.GetAccessRoles().Count == 0)
            throw new KavitaException("errors.oidc.role-not-assigned");

        user = await NewUserFromOpenIdConnect(settings, principal, externalId);
        if (user == null) return null;

        var roles = await userManager.GetRolesAsync(user);
        if (roles.Count == 0 || !roles.Contains(PolicyConstants.LoginRole))
            throw new KavitaException("errors.oidc.disabled-account");

        return user;
    }

    public async Task ClearOidcIds()
    {
        var users = await unitOfWork.UserRepository.GetAllUsersAsync();
        foreach (var user in users)
        {
            user.ExternalId = null;
        }

        await unitOfWork.CommitAsync();
    }

    private async Task<string?> FindBestAvailableName(ClaimsPrincipal claimsPrincipal)
    {
        var name = claimsPrincipal.FindFirstValue(JwtRegisteredClaimNames.PreferredUsername);
        if (await IsNameAvailable(name)) return name;

        name = claimsPrincipal.FindFirstValue(ClaimTypes.Name);
        if (await IsNameAvailable(name)) return name;

        name = claimsPrincipal.FindFirstValue(ClaimTypes.GivenName);
        if (await IsNameAvailable(name)) return name;

        name = claimsPrincipal.FindFirstValue(ClaimTypes.Surname);
        if (await IsNameAvailable(name)) return name;

        return null;
    }

    private async Task<bool> IsNameAvailable(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;

        return await userManager.FindByNameAsync(name) == null;
    }

    private async Task<AppUser?> NewUserFromOpenIdConnect(OidcConfigDto settings, ClaimsPrincipal claimsPrincipal, string externalId)
    {
        if (!settings.ProvisionAccounts) return null;

        var emailClaim = claimsPrincipal.FindFirst(ClaimTypes.Email);
        if (emailClaim == null || string.IsNullOrWhiteSpace(emailClaim.Value)) return null;

        var name = await FindBestAvailableName(claimsPrincipal) ?? emailClaim.Value;
        logger.LogInformation("Creating new user from OIDC: {Name} - {ExternalId}", name, externalId);

        // TODO: Move to account service, as we're sharing code with AccountController
        var user = new AppUserBuilder(name, emailClaim.Value,
            await unitOfWork.SiteThemeRepository.GetDefaultTheme()).Build();

        var res = await userManager.CreateAsync(user);
        if (!res.Succeeded)
        {
            logger.LogError("Failed to create new user from OIDC: {Errors}",
                res.Errors.Select(x => x.Description).ToList());
            throw new KavitaException("errors.oidc.creating-user");
        }

        if (settings.RequireVerifiedEmail)
        {
            // Email has been verified by OpenID Connect provider
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            await userManager.ConfirmEmailAsync(user, token);
        }

        user.ExternalId = externalId;
        user.Owner = AppUserOwner.OpenIdConnect;

        accountService.AddDefaultStreamsToUser(user);
        await accountService.AddDefaultReadingProfileToUser(user);

        await SyncUserSettings(settings, claimsPrincipal, user);
        await SetDefaults(settings, user);

        await unitOfWork.CommitAsync();
        return user;
    }

    private async Task SetDefaults(OidcConfigDto settings, AppUser user)
    {
        if (settings.SyncUserSettings) return;

        logger.LogDebug("Assigning defaults to newly created user; Roles: {Roles}, Libraries: {Libraries}, AgeRating: {AgeRating}, IncludeUnknowns: {IncludeUnknowns}",
            settings.DefaultRoles, settings.DefaultLibraries, settings.DefaultAgeRating, settings.DefaultIncludeUnknowns);

        // Assign roles
        var errors = await accountService.UpdateRolesForUser(user, settings.DefaultRoles);
        if (errors.Any()) throw new KavitaException("errors.oidc.syncing-user");

        // Assign libraries
        await accountService.UpdateLibrariesForUser(user, settings.DefaultLibraries, settings.DefaultRoles.Contains(PolicyConstants.AdminRole));

        // Assign age rating
        user.AgeRestriction = settings.DefaultAgeRating;
        user.AgeRestrictionIncludeUnknowns = settings.DefaultIncludeUnknowns;

        await unitOfWork.CommitAsync();
    }

    public async Task SyncUserSettings(OidcConfigDto settings, ClaimsPrincipal claimsPrincipal, AppUser user)
    {
        if (!settings.SyncUserSettings) return;

        // Never sync the default user
        var defaultAdminUser = await unitOfWork.UserRepository.GetDefaultAdminUser();
        if (defaultAdminUser.Id == user.Id) return;

        logger.LogInformation("Syncing user {UserName} from OIDC", user.UserName);
        await SyncRoles(claimsPrincipal, user);
        await SyncLibraries(claimsPrincipal, user);
        await SyncAgeRestriction(claimsPrincipal, user);


        if (unitOfWork.HasChanges())
            await unitOfWork.CommitAsync();
    }

    private async Task SyncRoles(ClaimsPrincipal claimsPrincipal, AppUser user)
    {
        var roles = claimsPrincipal.GetAccessRoles();
        logger.LogDebug("Syncing access roles for user {UserName}, found roles {Roles}", user.UserName, roles);

        var errors = await accountService.UpdateRolesForUser(user, roles);
        if (errors.Any()) throw new KavitaException("errors.oidc.syncing-user");
    }

    private async Task SyncLibraries(ClaimsPrincipal claimsPrincipal, AppUser user)
    {
        var libraryAccess = claimsPrincipal
            .FindAll(ClaimTypes.Role)
            .Where(r => r.Value.StartsWith(LibraryAccessPrefix))
            .Select(r => r.Value.TrimPrefix(LibraryAccessPrefix))
            .ToList();

        logger.LogDebug("Syncing libraries for user {UserName}, found library roles {Roles}", user.UserName, libraryAccess);

        var allLibraries = (await unitOfWork.LibraryRepository.GetLibrariesAsync()).ToList();
        var librariesIds = allLibraries.Where(l => libraryAccess.Contains(l.Name)).Select(l => l.Id).ToList();

        var hasAdminRole = await userManager.IsInRoleAsync(user, PolicyConstants.AdminRole);
        await accountService.UpdateLibrariesForUser(user, librariesIds, hasAdminRole);
    }

    private async Task SyncAgeRestriction(ClaimsPrincipal claimsPrincipal, AppUser user)
    {
        if (await userManager.IsInRoleAsync(user, PolicyConstants.AdminRole))
        {
            logger.LogInformation("User {UserName} is admin, granting access to all age ratings", user.UserName);
            user.AgeRestriction = AgeRating.NotApplicable;
            user.AgeRestrictionIncludeUnknowns = true;
            return;
        }

        var ageRatings = claimsPrincipal
            .FindAll(ClaimTypes.Role)
            .Where(r => r.Value.StartsWith(AgeRatingPrefix))
            .Select(r => r.Value.TrimPrefix(AgeRatingPrefix))
            .ToList();
        logger.LogDebug("Syncing age restriction for user {UserName}, found restrictions {Restrictions}", user.UserName, ageRatings);

        var highestAgeRating = AgeRating.Unknown;

        foreach (var ar in ageRatings)
        {
            if (!Enum.TryParse(ar, out AgeRating ageRating)) continue;

            if (ageRating > highestAgeRating)
            {
                highestAgeRating = ageRating;
            }
        }

        user.AgeRestriction = highestAgeRating;
        user.AgeRestrictionIncludeUnknowns = ageRatings.Contains(IncludeUnknowns);

        logger.LogDebug("Synced age restriction for user {UserName}, AgeRestriction {AgeRestriction}, IncludeUnknowns: {IncludeUnknowns}",
            user.UserName, ageRatings, user.AgeRestrictionIncludeUnknowns);
    }

}
