#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
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
using Flurl.Http;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace API.Services;

public interface IOidcService
{
    /// <summary>
    /// Returns the user authenticated with OpenID Connect
    /// </summary>
    /// <param name="request"></param>
    /// <param name="principal"></param>
    /// <returns></returns>
    /// <exception cref="KavitaException">if any requirements aren't met</exception>
    Task<AppUser?> LoginOrCreate(HttpRequest request, ClaimsPrincipal principal);
    /// <summary>
    /// Refresh the token inside the cookie when it's close to expiring. And sync the user
    /// </summary>
    /// <param name="ctx"></param>
    /// <returns></returns>
    /// <remarks>If the token is refreshed successfully, updates the last active time of the suer</remarks>
    Task<AppUser?> RefreshCookieToken(CookieValidatePrincipalContext ctx);
    /// <summary>
    /// Remove <see cref="AppUser.OidcId"/> from all users
    /// </summary>
    /// <returns></returns>
    Task ClearOidcIds();
}

/// <summary>
/// The ConfigurationManager will refresh the configuration periodically to ensure the data stays up to date
/// We can store the same one indefinitely as the authority does not change unless Kavita is restarted
/// </summary>
/// <remarks>The ConfigurationManager has its own lock, it loads data thread safe</remarks>
/// <remarks>It is registered as a singleton only if oidc is enabled. So must be nullable and optional</remarks>
public class OidcService(ILogger<OidcService> logger, UserManager<AppUser> userManager,
    IUnitOfWork unitOfWork, IAccountService accountService, IEmailService emailService,
    [FromServices] ConfigurationManager<OpenIdConnectConfiguration>? configurationManager = null): IOidcService
{
    public const string LibraryAccessPrefix = "library-";
    public const string AgeRestrictionPrefix = "age-restriction-";
    public const string IncludeUnknowns = "include-unknowns";
    public const string RefreshToken = "refresh_token";
    public const string IdToken = "id_token";
    public const string ExpiresAt = "expires_at";
    /// The name of the Auth Cookie set by .NET
    public const string CookieName = ".AspNetCore.Cookies";
    public static readonly List<string> DefaultScopes = ["openid", "profile", "offline_access", "roles", "email"];

    private static readonly ConcurrentDictionary<string, bool> RefreshInProgress = new();
    private static readonly ConcurrentDictionary<string, DateTimeOffset> LastFailedRefresh = new();

    public async Task<AppUser?> LoginOrCreate(HttpRequest request, ClaimsPrincipal principal)
    {
        var settings = (await unitOfWork.SettingsRepository.GetSettingsDtoAsync()).OidcConfig;

        var oidcId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(oidcId))
        {
            throw new KavitaException("errors.oidc.missing-external-id");
        }

        var user = await unitOfWork.UserRepository.GetByOidcId(oidcId, AppUserIncludes.UserPreferences | AppUserIncludes.SideNavStreams);
        if (user != null)
        {
            await SyncUserSettings(request, settings, principal, user);

            return user;
        }

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

            await SyncUserSettings(request, settings, principal, user);

            return user;
        }

        return await CreateNewAccount(request, principal, settings, oidcId);
    }

    public async Task<AppUser?> RefreshCookieToken(CookieValidatePrincipalContext ctx)
    {
        if (ctx.Principal == null) return null;

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(ctx.Principal.GetUserId()) ?? throw new UnauthorizedAccessException();
        var key = ctx.Principal.GetUsername();

        var refreshToken = ctx.Properties.GetTokenValue(RefreshToken);
        if (string.IsNullOrEmpty(refreshToken)) return user;

        var expiresAt = ctx.Properties.GetTokenValue(ExpiresAt);
        if (string.IsNullOrEmpty(expiresAt)) return user;

        // Do not spam refresh if it failed
        if (LastFailedRefresh.TryGetValue(key, out var time) && time.AddMinutes(30) < DateTimeOffset.UtcNow) return user;

        var tokenExpiry = DateTimeOffset.ParseExact(expiresAt, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        if (tokenExpiry >= DateTimeOffset.UtcNow.AddSeconds(30)) return user;

        // Ensure we're not refreshing twice
        if (!RefreshInProgress.TryAdd(key, true)) return user;

        try
        {
            var settings = (await unitOfWork.SettingsRepository.GetSettingsDtoAsync()).OidcConfig;

            var tokenResponse = await RefreshTokenAsync(settings, refreshToken);
            if (tokenResponse == null || !string.IsNullOrEmpty(tokenResponse.Error))
            {
                logger.LogTrace("Failed to refresh token : {Error} - {Description}", tokenResponse?.Error, tokenResponse?.ErrorDescription);
                LastFailedRefresh.TryAdd(key, DateTimeOffset.UtcNow);
                return user;
            }

            var newExpiresAt = DateTimeOffset.UtcNow.AddSeconds(double.Parse(tokenResponse.ExpiresIn));
            ctx.Properties.UpdateTokenValue(ExpiresAt, newExpiresAt.ToString("o"));
            ctx.Properties.UpdateTokenValue(RefreshToken, tokenResponse.RefreshToken);
            ctx.Properties.UpdateTokenValue(IdToken, tokenResponse.IdToken);
            ctx.ShouldRenew = true;

            if (string.IsNullOrEmpty(tokenResponse.IdToken))
            {
                logger.LogTrace("The OIDC provider did not return an id token in the refresh response, continuous sync is not supported");
                return user;
            }

            await SyncUserSettings(ctx, settings, tokenResponse.IdToken, user);
            logger.LogTrace("Automatically refreshed token for user {UserId}", ctx.Principal?.GetUserId());
        }
        finally
        {
            RefreshInProgress.TryRemove(key, out _);
            LastFailedRefresh.TryRemove(key, out _);
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
    /// Tries to construct a new account from the OIDC Principal, may fail if required conditions aren't met
    /// </summary>
    /// <param name="request"></param>
    /// <param name="principal"></param>
    /// <param name="settings"></param>
    /// <param name="oidcId"></param>
    /// <returns></returns>
    /// <exception cref="KavitaException"></exception>
    private async Task<AppUser?> CreateNewAccount(HttpRequest request, ClaimsPrincipal principal, OidcConfigDto settings, string oidcId)
    {
        // Check if the token contains the login role, or the admin role
        var isAllowedToBeCreated = principal.GetClaimsWithPrefix(settings.RolesClaim, settings.RolesPrefix)
            .Intersect([PolicyConstants.LoginRole, PolicyConstants.AdminRole], StringComparer.OrdinalIgnoreCase)
            .Any();

        if (settings.SyncUserSettings && !isAllowedToBeCreated)
        {
            logger.LogDebug("Login role was not found under claim {Claim} with prefix {Prefix}", settings.RolesClaim, settings.RolesPrefix);
            throw new KavitaException("errors.oidc.role-not-assigned");
        }

        try
        {
            return await NewUserFromOpenIdConnect(request, settings, principal, oidcId);
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

    }

    /// <summary>
    /// Find the best available name from claims
    /// </summary>
    /// <param name="claimsPrincipal"></param>
    /// <param name="orEqualTo">Also return if the claim is equal to this value</param>
    /// <returns></returns>
    public async Task<string?> FindBestAvailableName(ClaimsPrincipal claimsPrincipal, string? orEqualTo = null)
    {
        var nameCandidates = new[]
        {
            claimsPrincipal.FindFirstValue(JwtRegisteredClaimNames.PreferredUsername),
            claimsPrincipal.FindFirstValue(ClaimTypes.Name),
            claimsPrincipal.FindFirstValue(ClaimTypes.GivenName),
            claimsPrincipal.FindFirstValue(ClaimTypes.Surname)
        };

        foreach (var name in nameCandidates.Where(n => !string.IsNullOrEmpty(n)))
        {
            if (name == orEqualTo || await IsNameAvailable(name))
            {
                return name;
            }
        }

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
        logger.LogInformation("Creating new user from OIDC: {Name} - {ExternalId}", name.Censor(), externalId);

        var user = new AppUserBuilder(name, emailClaim.Value, await unitOfWork.SiteThemeRepository.GetDefaultTheme()).Build();

        var res = await userManager.CreateAsync(user);
        if (!res.Succeeded)
        {
            logger.LogError("Failed to create new user from OIDC: {Errors}", res.Errors.Select(x => x.Description).ToList());
            throw new KavitaException("errors.oidc.creating-user");
        }

        if (claimsPrincipal.HasVerifiedEmail())
        {
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            await userManager.ConfirmEmailAsync(user, token);
        }

        user.OidcId = externalId;
        user.IdentityProvider = IdentityProvider.OpenIdConnect;

        await accountService.SeedUser(user);

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

        // Assign age rating, or bypass if admin
        if (await userManager.IsInRoleAsync(user, PolicyConstants.AdminRole))
        {
            user.AgeRestriction = AgeRating.NotApplicable;
            user.AgeRestrictionIncludeUnknowns = true;
        }
        else
        {
            user.AgeRestriction = settings.DefaultAgeRestriction;
            user.AgeRestrictionIncludeUnknowns = settings.DefaultIncludeUnknowns;
        }

        await unitOfWork.CommitAsync();
    }

    /// <summary>
    /// Syncs the given user to the principal found in the id token
    /// </summary>
    /// <param name="ctx"></param>
    /// <param name="settings"></param>
    /// <param name="idToken"></param>
    /// <param name="user"></param>
    /// <exception cref="UnauthorizedAccessException">If syncing fails</exception>
    private async Task SyncUserSettings(CookieValidatePrincipalContext ctx, OidcConfigDto settings, string idToken, AppUser user)
    {
        if (!settings.SyncUserSettings || user.IdentityProvider != IdentityProvider.OpenIdConnect) return;

        try
        {
            var newPrincipal = await ParseIdToken(settings, idToken);
            if (newPrincipal == null)
            {
                throw new KavitaException("errors.oidc.no-account");
            }
            await SyncUserSettings(ctx.HttpContext.Request, settings, newPrincipal, user);
        }
        catch (KavitaException ex)
        {
            logger.LogError(ex, "Failed to sync user after token refresh");
            throw new UnauthorizedAccessException(ex.Message);
        }
    }

    /// <summary>
    /// Updates roles, library access and age rating restriction. Will not modify the default admin
    /// </summary>
    /// <param name="request"></param>
    /// <param name="settings"></param>
    /// <param name="claimsPrincipal"></param>
    /// <param name="user"></param>
    public async Task SyncUserSettings(HttpRequest request, OidcConfigDto settings, ClaimsPrincipal claimsPrincipal, AppUser user)
    {
        if (!settings.SyncUserSettings || user.IdentityProvider != IdentityProvider.OpenIdConnect) return;

        // Never sync the default user
        var defaultAdminUser = await unitOfWork.UserRepository.GetDefaultAdminUser();
        if (defaultAdminUser.Id == user.Id) return;

        logger.LogDebug("Syncing user {UserId} from OIDC", user.Id);
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
            logger.LogError(ex, "Failed to sync user {UserId} from OIDC", user.Id);
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
                logger.LogError("Failed to update email for user {UserId} from OIDC {Errors}", user.Id, res.Errors.Select(x => x.Description).ToList());
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
        logger.LogCritical("[Update Email]: Automatic email update after OIDC sync, email Link for {UserId}: {Link}", user.Id, emailLink);

        if (!shouldEmailUser)
        {
            logger.LogInformation("Cannot email admin, email not setup or admin email invalid");
            return;
        }

        if (!isValidEmailAddress)
        {
            logger.LogCritical("[Update Email]: User is trying to update their email, but their existing email ({Email}) isn't valid. No email will be send", user.Email.Censor());
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
            logger.LogError("Failed to update username for user {UserId} to {NewUserName} from OIDC {Errors}", user.Id,
                bestName.Censor(),  res.Errors.Select(x => x.Description).ToList());
            throw new KavitaException("errors.oidc.failed-to-update-username");
        }
    }

    private async Task SyncRoles(OidcConfigDto settings, ClaimsPrincipal claimsPrincipal, AppUser user)
    {
        var rolesFromToken = claimsPrincipal.GetClaimsWithPrefix(settings.RolesClaim, settings.RolesPrefix);

        var roles = PolicyConstants.ValidRoles
            .Where(s => rolesFromToken.Contains(s, StringComparer.OrdinalIgnoreCase))
            .ToList();

        logger.LogDebug("Syncing access roles for user {UserId}, found roles {Roles}", user.Id, roles);

        var errors = (await accountService.UpdateRolesForUser(user, roles)).ToList();
        if (errors.Any())
        {
            logger.LogError("Failed to sync roles {Errors}", errors.Select(x => x.Description).ToList());
            throw new KavitaException("errors.oidc.syncing-user");
        }
    }

    private async Task SyncLibraries(OidcConfigDto settings, ClaimsPrincipal claimsPrincipal, AppUser user)
    {
        var libraryAccessPrefix = settings.RolesPrefix + LibraryAccessPrefix;
        var libraryAccess = claimsPrincipal.GetClaimsWithPrefix(settings.RolesClaim, libraryAccessPrefix);

        logger.LogDebug("Syncing libraries for user {UserId}, found library roles {Roles}", user.Id, libraryAccess);

        var allLibraries = (await unitOfWork.LibraryRepository.GetLibrariesAsync()).ToList();
        // Distinct to ensure each library (id) is only present once
        var librariesIds = allLibraries
            .Where(l => libraryAccess.Contains(l.Name, StringComparer.OrdinalIgnoreCase))
            .Select(l => l.Id).Distinct()
            .ToList();

        var hasAdminRole = await userManager.IsInRoleAsync(user, PolicyConstants.AdminRole);
        await accountService.UpdateLibrariesForUser(user, librariesIds, hasAdminRole);
    }

    private async Task SyncAgeRestriction(OidcConfigDto settings, ClaimsPrincipal claimsPrincipal, AppUser user)
    {
        if (await userManager.IsInRoleAsync(user, PolicyConstants.AdminRole))
        {
            logger.LogDebug("User {UserId} is admin, granting access to all age ratings", user.Id);
            user.AgeRestriction = AgeRating.NotApplicable;
            user.AgeRestrictionIncludeUnknowns = true;
            return;
        }

        var ageRatingPrefix = settings.RolesPrefix + AgeRestrictionPrefix;
        var ageRatings = claimsPrincipal.GetClaimsWithPrefix(settings.RolesClaim, ageRatingPrefix);
        logger.LogDebug("Syncing age restriction for user {UserId}, found restrictions {Restrictions}", user.Id, ageRatings);

        if (ageRatings.Count == 0 || (ageRatings.Count == 1 && ageRatings.Contains(IncludeUnknowns, StringComparer.OrdinalIgnoreCase)))
        {
            logger.LogDebug("No age restriction found in roles, setting to NotApplicable and Include Unknowns: {IncludeUnknowns}", settings.DefaultIncludeUnknowns);

            user.AgeRestriction = AgeRating.NotApplicable;
            user.AgeRestrictionIncludeUnknowns = true;
            return;
        }

        var highestAgeRestriction = AgeRating.NotApplicable;

        foreach (var ar in ageRatings)
        {
            if (ar.Equals(IncludeUnknowns, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!EnumExtensions.TryParse(ar, out AgeRating ageRating))
            {
                logger.LogDebug("Age Restriction role configured that failed to map to a known age rating: {RoleName}", AgeRestrictionPrefix+ar);
                continue;
            }

            if (ageRating > highestAgeRestriction)
            {
                highestAgeRestriction = ageRating;
            }
        }

        user.AgeRestriction = highestAgeRestriction;
        user.AgeRestrictionIncludeUnknowns = ageRatings.Contains(IncludeUnknowns, StringComparer.OrdinalIgnoreCase);

        logger.LogDebug("Synced age restriction for user {UserId}, AgeRestriction {AgeRestriction}, IncludeUnknowns: {IncludeUnknowns}",
            user.Id, user.AgeRestriction, user.AgeRestrictionIncludeUnknowns);
    }

    /// <summary>
    /// Loads the discovery document if not already loaded, then refreshed the tokens for the user
    /// </summary>
    /// <param name="dto"></param>
    /// <param name="refreshToken"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    private async Task<OpenIdConnectMessage?> RefreshTokenAsync(OidcConfigDto dto, string refreshToken)
    {
        if (configurationManager == null)
        {
            return null; // never happens failsafe
        }

        var discoveryDocument = await configurationManager.GetConfigurationAsync();

        var msg = new
        {
            grant_type = RefreshToken,
            refresh_token = refreshToken,
            client_id = dto.ClientId,
            client_secret = dto.Secret,
        };

        var json = await discoveryDocument.TokenEndpoint
            .AllowAnyHttpStatus()
            .PostUrlEncodedAsync(msg)
            .ReceiveString();

        return new OpenIdConnectMessage(json);
    }

    /// <summary>
    /// Loads the discovery document if not already loaded, then parses the given id token securely
    /// </summary>
    /// <param name="dto"></param>
    /// <param name="idToken"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    private async Task<ClaimsPrincipal?> ParseIdToken(OidcConfigDto dto, string idToken)
    {
        if (configurationManager == null)
        {
            return null; // never happens failsafe
        }

        var discoveryDocument = await configurationManager.GetConfigurationAsync();

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = discoveryDocument.Issuer,
            ValidAudience = dto.ClientId,
            IssuerSigningKeys = discoveryDocument.SigningKeys,
            ValidateIssuerSigningKey = true,
        };

        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(idToken, tokenValidationParameters, out _);

        return principal;
    }

    /// <summary>
    /// Return a list of claims in the same way the NativeJWT token would map them.
    /// Optionally include original claims if the claims are needed later in the pipeline
    /// </summary>
    /// <param name="services"></param>
    /// <param name="principal"></param>
    /// <param name="user"></param>
    /// <param name="includeOriginalClaims"></param>
    /// <returns></returns>
    public static async Task<List<Claim>> ConstructNewClaimsList(IServiceProvider services, ClaimsPrincipal? principal, AppUser user, bool includeOriginalClaims = true)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Name, user.UserName ?? string.Empty),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
        };

        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var roles = await userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        if (includeOriginalClaims)
        {
            claims.AddRange(principal?.Claims ?? []);
        }

        return claims;
    }

}
