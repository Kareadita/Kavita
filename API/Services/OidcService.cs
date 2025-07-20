#nullable enable
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Email;
using API.DTOs.Settings;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers.Builders;
using Hangfire;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Microsoft.AspNetCore.Http;
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
    Task<AppUser?> LoginOrCreate(HttpRequest request, ClaimsPrincipal principal);
    /// <summary>
    /// Updates roles, library access and age rating restriction. Will not modify the default admin
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="claimsPrincipal"></param>
    /// <param name="user"></param>
    Task SyncUserSettings(HttpRequest request, OidcConfigDto settings, ClaimsPrincipal claimsPrincipal, AppUser user);
    /// <summary>
    /// Remove <see cref="AppUser.OidcId"/> from all users
    /// </summary>
    /// <returns></returns>
    Task ClearOidcIds();
}

public class OidcService(ILogger<OidcService> logger, UserManager<AppUser> userManager,
    IUnitOfWork unitOfWork, IAccountService accountService, IEmailService emailService): IOidcService
{
    public const string LibraryAccessPrefix = "library-";
    public const string AgeRestrictionPrefix = "age-restriction-";
    public const string IncludeUnknowns = "include-unknowns";

    public async Task<AppUser?> LoginOrCreate(HttpRequest request, ClaimsPrincipal principal)
    {
        var settings = (await unitOfWork.SettingsRepository.GetSettingsDtoAsync()).OidcConfig;

        var oidcId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(oidcId))
        {
            throw new KavitaException("errors.oidc.missing-external-id");
        }

        var user = await unitOfWork.UserRepository.GetByOidcId(oidcId, AppUserIncludes.UserPreferences);
        if (user != null) return user;

        var email = principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email))
        {
            throw new KavitaException("errors.oidc.missing-email");
        }

        if (settings.RequireVerifiedEmail && !principal.HasVerifiedEmail())
        {
            throw new KavitaException("errors.oidc.email-not-verified");
        }


        user = await unitOfWork.UserRepository.GetUserByEmailAsync(email, AppUserIncludes.UserPreferences | AppUserIncludes.SideNavStreams);
        if (user != null)
        {
            // Don't allow taking over accounts
            // This could happen if the user changes their email in OIDC, and then someone else uses the old one
            if (!string.IsNullOrEmpty(user.OidcId))
            {
                throw new KavitaException("errors.oidc.email-in-use");
            }

            logger.LogDebug("User {UserName} has matched on email to {OidcId}", user.Id, oidcId);
            user.OidcId = oidcId;
            await unitOfWork.CommitAsync();

            return user;
        }

        // Cannot match on native account, try and create new one
        var accessRoles = principal.GetClaimsWithPrefix(settings.RolesClaim, settings.RolesPrefix)
            .Where(s => PolicyConstants.ValidRoles.Contains(s)).ToList();
        if (settings.SyncUserSettings && accessRoles.Count == 0)
        {
            throw new KavitaException("errors.oidc.role-not-assigned");
        }

        try
        {
            user = await NewUserFromOpenIdConnect(request, settings, principal, oidcId);
        }
        catch (KavitaException e)
        {
            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occured creating a new user");
            throw new KavitaException("errors.oidc.creating-user");
        }

        if (user == null) return null;

        var roles = await userManager.GetRolesAsync(user);
        if (roles.Count == 0 || !roles.Contains(PolicyConstants.LoginRole))
        {
            throw new KavitaException("errors.oidc.disabled-account");
        }

        return user;
    }

    public async Task ClearOidcIds()
    {
        var users = await unitOfWork.UserRepository.GetAllUsersAsync();
        foreach (var user in users)
        {
            user.OidcId = null;
        }

        await unitOfWork.CommitAsync();
    }

    /// <summary>
    /// Find the best available name from claims
    /// </summary>
    /// <param name="claimsPrincipal"></param>
    /// <param name="orEqualTo">Also return if the claim is equal to this value</param>
    /// <returns></returns>
    public async Task<string?> FindBestAvailableName(ClaimsPrincipal claimsPrincipal, string? orEqualTo = null)
    {
        var name = claimsPrincipal.FindFirstValue(JwtRegisteredClaimNames.PreferredUsername);
        if (orEqualTo == name || await IsNameAvailable(name)) return name;

        name = claimsPrincipal.FindFirstValue(ClaimTypes.Name);
        if (orEqualTo == name || await IsNameAvailable(name)) return name;

        name = claimsPrincipal.FindFirstValue(ClaimTypes.GivenName);
        if (orEqualTo == name || await IsNameAvailable(name)) return name;

        name = claimsPrincipal.FindFirstValue(ClaimTypes.Surname);
        if (orEqualTo == name || await IsNameAvailable(name)) return name;

        return null;
    }

    private async Task<bool> IsNameAvailable(string? name)
    {
        return !(await accountService.ValidateUsername(name)).Any();
    }

    private async Task<AppUser?> NewUserFromOpenIdConnect(HttpRequest request, OidcConfigDto settings, ClaimsPrincipal claimsPrincipal, string externalId)
    {
        if (!settings.ProvisionAccounts) return null;

        var emailClaim = claimsPrincipal.FindFirst(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(emailClaim?.Value)) return null;

        var name = await FindBestAvailableName(claimsPrincipal) ?? emailClaim.Value;
        logger.LogInformation("Creating new user from OIDC: {Name} - {ExternalId}", name, externalId);

        var user = new AppUserBuilder(name, emailClaim.Value,
            await unitOfWork.SiteThemeRepository.GetDefaultTheme()).Build();

        var res = await userManager.CreateAsync(user);
        if (!res.Succeeded)
        {
            logger.LogError("Failed to create new user from OIDC: {Errors}",
                res.Errors.Select(x => x.Description).ToList());
            throw new KavitaException("errors.oidc.creating-user");
        }

        if (claimsPrincipal.HasVerifiedEmail())
        {
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            await userManager.ConfirmEmailAsync(user, token);
        }

        user.OidcId = externalId;
        user.IdentityProvider = IdentityProvider.OpenIdConnect;

        accountService.AddDefaultStreamsToUser(user);
        await accountService.AddDefaultReadingProfileToUser(user);

        await SyncUserSettings(request, settings, claimsPrincipal, user);
        await SetDefaults(settings, user);

        await unitOfWork.CommitAsync();

        return user;
    }

    /// <summary>
    /// Assign configured defaults (libraries, age ratings, roles) to the newly created user
    /// </summary>
    private async Task SetDefaults(OidcConfigDto settings, AppUser user)
    {
        if (settings.SyncUserSettings) return;

        logger.LogDebug("Assigning defaults to newly created user; Roles: {Roles}, Libraries: {Libraries}, AgeRating: {AgeRating}, IncludeUnknowns: {IncludeUnknowns}",
            settings.DefaultRoles, settings.DefaultLibraries, settings.DefaultAgeRestriction, settings.DefaultIncludeUnknowns);

        // Assign roles
        var errors = await accountService.UpdateRolesForUser(user, settings.DefaultRoles);
        if (errors.Any()) throw new KavitaException("errors.oidc.syncing-user");

        // Assign libraries
        await accountService.UpdateLibrariesForUser(user, settings.DefaultLibraries, settings.DefaultRoles.Contains(PolicyConstants.AdminRole));

        // Assign age rating
        user.AgeRestriction = settings.DefaultAgeRestriction;
        user.AgeRestrictionIncludeUnknowns = settings.DefaultIncludeUnknowns;

        await unitOfWork.CommitAsync();
    }

    public async Task SyncUserSettings(HttpRequest request, OidcConfigDto settings, ClaimsPrincipal claimsPrincipal, AppUser user)
    {
        if (!settings.SyncUserSettings) return;

        // Never sync the default user
        var defaultAdminUser = await unitOfWork.UserRepository.GetDefaultAdminUser();
        if (defaultAdminUser.Id == user.Id) return;

        logger.LogDebug("Syncing user {UserName} from OIDC", user.UserName);
        try
        {

            await SyncEmail(request, settings, claimsPrincipal, user);
            await SyncUsername(claimsPrincipal, user);
            await SyncRoles(settings, claimsPrincipal, user);
            await SyncLibraries(settings, claimsPrincipal, user);
            await SyncAgeRestriction(settings, claimsPrincipal, user);

            if (unitOfWork.HasChanges())
            {
                await unitOfWork.CommitAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync user {UserName} from OIDC", user.UserName);
            await unitOfWork.RollbackAsync();
            throw new KavitaException("errors.oidc.syncing-user", ex);
        }
    }

    private async Task SyncEmail(HttpRequest request, OidcConfigDto settings, ClaimsPrincipal claimsPrincipal, AppUser user)
    {
        var email = claimsPrincipal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email) || user.Email == email) return;

        if (settings.RequireVerifiedEmail && !claimsPrincipal.HasVerifiedEmail())
        {
            throw new KavitaException("errors.oidc.email-not-verified");
        }

        // Ensure no other user uses this email
        var other = await userManager.FindByEmailAsync(email);
        if (other != null)
        {
            throw new KavitaException("errors.oidc.email-in-use");
        }

        // The email is verified, we can go ahead and change & confirm it
        if (claimsPrincipal.HasVerifiedEmail())
        {
            var res = await userManager.SetEmailAsync(user, email);
            if (!res.Succeeded)
            {
                logger.LogError("Failed to update email for user {UserName} from OIDC {Errors}", user.UserName, res.Errors.Select(x => x.Description).ToList());
                throw new KavitaException("errors.oidc.failed-to-update-email");
            }

            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
            return;
        }

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var isValidEmailAddress = !string.IsNullOrEmpty(user.Email) && emailService.IsValidEmail(user.Email);
        var isEmailSetup = (await unitOfWork.SettingsRepository.GetSettingsDtoAsync()).IsEmailSetup();
        var shouldEmailUser = isEmailSetup || !isValidEmailAddress;

        user.EmailConfirmed = !shouldEmailUser;
        user.ConfirmationToken = token;
        await userManager.UpdateAsync(user);

        var emailLink = await emailService.GenerateEmailLink(request, user.ConfirmationToken, "confirm-email-update", email);
        logger.LogCritical("[Update Email]: Automatic email update after OIDC sync, email Link for {UserName}: {Link}", user.UserName, emailLink);

        if (!shouldEmailUser)
        {
            logger.LogInformation("Cannot email admin, email not setup or admin email invalid");
            return;
        }

        if (!isValidEmailAddress)
        {
            logger.LogCritical("[Update Email]: User is trying to update their email, but their existing email ({Email}) isn't valid. No email will be send", user.Email);
            return;
        }

        try
        {
            var invitingUser = await unitOfWork.UserRepository.GetDefaultAdminUser();
            BackgroundJob.Enqueue(() => emailService.SendEmailChangeEmail(new ConfirmationEmailDto()
            {
                EmailAddress = string.IsNullOrEmpty(user.Email) ? email : user.Email,
                InstallId = BuildInfo.Version.ToString(),
                InvitingUser = invitingUser.UserName,
                ServerConfirmationLink = emailLink,
            }));
        }
        catch (Exception)
        {
            /* Swallow exception */
        }

    }

    private async Task SyncUsername(ClaimsPrincipal claimsPrincipal, AppUser user)
    {
        var bestName = await FindBestAvailableName(claimsPrincipal, user.UserName);
        if (bestName == null || bestName == user.UserName) return;

        var res = await userManager.SetUserNameAsync(user, bestName);
        if (!res.Succeeded)
        {
            logger.LogError("Failed to update username for user {UserName} to {NewUserName} from OIDC {Errors}", user.UserName, bestName,  res.Errors.Select(x => x.Description).ToList());
            throw new KavitaException("errors.oidc.failed-to-update-username");
        }
    }

    private async Task SyncRoles(OidcConfigDto settings, ClaimsPrincipal claimsPrincipal, AppUser user)
    {
        var roles = claimsPrincipal.GetClaimsWithPrefix(settings.RolesClaim, settings.RolesPrefix)
            .Where(s => PolicyConstants.ValidRoles.Contains(s)).ToList();
        logger.LogDebug("Syncing access roles for user {UserName}, found roles {Roles}", user.UserName, roles);

        var errors = await accountService.UpdateRolesForUser(user, roles);
        if (errors.Any()) throw new KavitaException("errors.oidc.syncing-user");
    }

    private async Task SyncLibraries(OidcConfigDto settings, ClaimsPrincipal claimsPrincipal, AppUser user)
    {
        var libraryAccessPrefix = settings.RolesPrefix + LibraryAccessPrefix;
        var libraryAccess = claimsPrincipal.GetClaimsWithPrefix(settings.RolesClaim, libraryAccessPrefix);

        logger.LogDebug("Syncing libraries for user {UserName}, found library roles {Roles}", user.UserName, libraryAccess);

        var allLibraries = (await unitOfWork.LibraryRepository.GetLibrariesAsync()).ToList();
        // Distinct to ensure each library (id) is only present once
        var librariesIds = allLibraries.Where(l => libraryAccess.Contains(l.Name)).Select(l => l.Id).Distinct().ToList();

        var hasAdminRole = await userManager.IsInRoleAsync(user, PolicyConstants.AdminRole);
        await accountService.UpdateLibrariesForUser(user, librariesIds, hasAdminRole);
    }

    private async Task SyncAgeRestriction(OidcConfigDto settings, ClaimsPrincipal claimsPrincipal, AppUser user)
    {
        if (await userManager.IsInRoleAsync(user, PolicyConstants.AdminRole))
        {
            logger.LogDebug("User {UserName} is admin, granting access to all age ratings", user.UserName);
            user.AgeRestriction = AgeRating.NotApplicable;
            user.AgeRestrictionIncludeUnknowns = true;
            return;
        }

        var ageRatingPrefix = settings.RolesPrefix + AgeRestrictionPrefix;
        var ageRatings = claimsPrincipal.GetClaimsWithPrefix(settings.RolesClaim, ageRatingPrefix);
        logger.LogDebug("Syncing age restriction for user {UserName}, found restrictions {Restrictions}", user.UserName, ageRatings);

        if (ageRatings.Count == 0 || (ageRatings.Count == 1 && ageRatings.Contains(IncludeUnknowns)))
        {
            logger.LogDebug("No age restriction found in roles, setting to RatingPending");

            user.AgeRestriction = AgeRating.NotApplicable;
            user.AgeRestrictionIncludeUnknowns = true;
            return;
        }

        var highestAgeRestriction = AgeRating.NotApplicable;

        foreach (var ar in ageRatings)
        {
            if (!EnumExtensions.TryParse(ar, out AgeRating ageRating))
            {
                logger.LogDebug("Age Restriction role configured that failed to map to a known age rating: {RoleName}", OidcService.AgeRestrictionPrefix+ar);
                continue;
            }

            if (ageRating > highestAgeRestriction)
            {
                highestAgeRestriction = ageRating;
            }
        }

        user.AgeRestriction = highestAgeRestriction;
        user.AgeRestrictionIncludeUnknowns = ageRatings.Contains(IncludeUnknowns);

        logger.LogDebug("Synced age restriction for user {UserName}, AgeRestriction {AgeRestriction}, IncludeUnknowns: {IncludeUnknowns}",
            user.UserName, user.AgeRestriction, user.AgeRestrictionIncludeUnknowns);
    }

}
