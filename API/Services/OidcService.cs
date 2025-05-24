#nullable enable
using System;
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
using AutoMapper;
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
}

public class OidcService(ILogger<OidcService> logger, UserManager<AppUser> userManager,
    IUnitOfWork unitOfWork, IMapper mapper, IAccountService accountService): IOidcService
{
    private const string LibraryAccessClaim = "library";
    private const string AgeRatingClaim = "AgeRating";

    public async Task<AppUser?> LoginOrCreate(ClaimsPrincipal principal)
    {
        var settings = (await unitOfWork.SettingsRepository.GetSettingsDtoAsync()).OidcConfig;

        var externalId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(externalId))
            throw new KavitaException("oidc.errors.missing-external-id");

        var user = await unitOfWork.UserRepository.GetByExternalId(externalId, AppUserIncludes.UserPreferences);
        if (user != null)
        {
            // await ProvisionUserSettings(settings, principal, user);
            return user;
        }

        var email = principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email))
            throw new KavitaException("oidc.errors.missing-email");

        if (settings.RequireVerifiedEmail && !principal.HasVerifiedEmail())
            throw new KavitaException("oidc.errors.email-not-verified");


        user = await unitOfWork.UserRepository.GetUserByEmailAsync(email, AppUserIncludes.UserPreferences)
               ?? await NewUserFromOpenIdConnect(settings, principal);
        if (user == null) return null;

        user.ExternalId = externalId;

        // await ProvisionUserSettings(settings, principal, user);

        var roles = await userManager.GetRolesAsync(user);
        if (roles.Count > 0 && !roles.Contains(PolicyConstants.LoginRole))
            throw new KavitaException("oidc.errors.disabled-account");

        return user;
    }

    private async Task<AppUser?> NewUserFromOpenIdConnect(OidcConfigDto settings, ClaimsPrincipal claimsPrincipal)
    {
        if (!settings.ProvisionAccounts) return null;

        var emailClaim = claimsPrincipal.FindFirst(ClaimTypes.Email);
        if (emailClaim == null || string.IsNullOrWhiteSpace(emailClaim.Value)) return null;

        var name = claimsPrincipal.FindFirstValue(ClaimTypes.Name);
        name ??= claimsPrincipal.FindFirstValue(ClaimTypes.GivenName);
        name ??= claimsPrincipal.FindFirstValue(ClaimTypes.Surname);
        name ??= emailClaim.Value;

        var other = await unitOfWork.UserRepository.GetUserByUsernameAsync(name);
        if (other != null)
        {
            // We match by email, so this will always be unique
            name = emailClaim.Value;
        }

        // TODO: Move to account service, as we're sharing code with AccountController
        var user = new AppUserBuilder(name, emailClaim.Value,
            await unitOfWork.SiteThemeRepository.GetDefaultTheme()).Build();

        var res = await userManager.CreateAsync(user);
        if (!res.Succeeded)
        {
            logger.LogError("Failed to create new user from OIDC: {Errors}",
                res.Errors.Select(x => x.Description).ToString());
            throw new KavitaException("oidc.errors.creating-user");
        }

        AddDefaultStreamsToUser(user, mapper);

        if (settings.RequireVerifiedEmail)
        {
            // Email has been verified by OpenID Connect provider
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            await userManager.ConfirmEmailAsync(user, token);
        }

        await userManager.AddToRoleAsync(user, PolicyConstants.LoginRole);

        await unitOfWork.CommitAsync();
        return user;
    }

    /// <summary>
    /// Updates roles, library access and age rating. Does not assign admin role, or to admin roles
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="claimsPrincipal"></param>
    /// <param name="user"></param>
    /// <remarks>Extra feature, little buggy for now</remarks>
    private async Task SyncUserSettings(OidcConfigDto settings, ClaimsPrincipal claimsPrincipal, AppUser user)
    {
        if (!settings.ProvisionUserSettings) return;

        var userRoles = await userManager.GetRolesAsync(user);
        if (userRoles.Contains(PolicyConstants.AdminRole)) return;

        await SyncRoles(claimsPrincipal, user);
        await SyncLibraries(claimsPrincipal, user);
        SyncAgeRating(claimsPrincipal, user);

        if (unitOfWork.HasChanges())
            await unitOfWork.CommitAsync();
    }

    private async Task SyncRoles(ClaimsPrincipal claimsPrincipal, AppUser user)
    {
        var roles = claimsPrincipal.FindAll(ClaimTypes.Role)
            .Select(r => r.Value)
            .Where(r => PolicyConstants.ValidRoles.Contains(r))
            .Where(r => r != PolicyConstants.AdminRole)
            .ToList();
        if (roles.Count == 0) return;

        var errors = await accountService.UpdateRolesForUser(user, roles);
        if (errors.Any()) throw new KavitaException("oidc.errors.syncing-user");
    }

    private async Task SyncLibraries(ClaimsPrincipal claimsPrincipal, AppUser user)
    {
        var libraryAccess = claimsPrincipal
            .FindAll(LibraryAccessClaim)
            .Select(r => r.Value)
            .ToList();
        if (libraryAccess.Count == 0) return;

        var allLibraries = (await unitOfWork.LibraryRepository.GetLibrariesAsync()).ToList();
        var librariesIds = allLibraries.Where(l => libraryAccess.Contains(l.Name)).Select(l => l.Id).ToList();
        var hasAdminRole = await userManager.IsInRoleAsync(user, PolicyConstants.AdminRole);

        await accountService.UpdateLibrariesForUser(user, librariesIds, hasAdminRole);
    }

    private static void SyncAgeRating(ClaimsPrincipal claimsPrincipal, AppUser user)
    {

        var ageRatings = claimsPrincipal
            .FindAll(AgeRatingClaim)
            .Select(r => r.Value)
            .ToList();
        if (ageRatings.Count == 0) return;

        var highestAgeRating = AgeRating.NotApplicable;

        foreach (var ar in ageRatings)
        {
            if (!Enum.TryParse(ar, out AgeRating ageRating)) continue;

            if (ageRating > highestAgeRating)
            {
                highestAgeRating = ageRating;
            }
        }

        user.AgeRestriction = highestAgeRating;
    }

    // DUPLICATED CODE
    private static void AddDefaultStreamsToUser(AppUser user, IMapper mapper)
    {
        foreach (var newStream in Seed.DefaultStreams.Select(mapper.Map<AppUserDashboardStream, AppUserDashboardStream>))
        {
            user.DashboardStreams.Add(newStream);
        }

        foreach (var stream in Seed.DefaultSideNavStreams.Select(mapper.Map<AppUserSideNavStream, AppUserSideNavStream>))
        {
            user.SideNavStreams.Add(stream);
        }
    }
}
