#nullable enable

using System.Collections.Generic;
using System.Security.Claims;
using API.Entities.Enums;

namespace API.DTOs.Settings;

/**
 All configuration regarding OIDC
 */
public sealed record OidcConfigDto: OidcPublicConfigDto
{
    /// <summary>
    /// If true, auto creates a new account when someone logs in via OpenID Connect
    /// </summary>
    public bool ProvisionAccounts { get; set; }
    /// <summary>
    /// Require emails to be verified by the OpenID Connect provider when creating accounts on login
    /// </summary>
    public bool RequireVerifiedEmail { get; set; } = true;
    /// <summary>
    /// Overwrite Kavita roles, libraries and age rating with OpenIDConnect provided roles on log in.
    /// </summary>
    public bool SyncUserSettings { get; set; }
    /// <summary>
    /// A prefix that all roles Kavita check for during sync must have
    /// </summary>
    public string RolesPrefix { get; set; } = string.Empty;
    /// <summary>
    /// The JWT claim roles are mapped under, defaults to <see cref="ClaimTypes.Role"/>
    /// </summary>
    public string RolesClaim { get; set; } = ClaimTypes.Role;

    // Default values used when SyncUserSettings is false
    #region Default user settings

    public List<string> DefaultRoles { get; set; } = [];
    public List<int> DefaultLibraries { get; set; } = [];
    public AgeRating DefaultAgeRestriction { get; set; } = AgeRating.Unknown;
    public bool DefaultIncludeUnknowns { get; set; } = false;

    #endregion


    /// <summary>
    /// Returns true if the <see cref="OidcPublicConfigDto.Authority"/> has been set
    /// </summary>
    public bool Enabled => !string.IsNullOrEmpty(Authority);
}
