#nullable enable

using System.Collections.Generic;
using System.Security.Claims;
using API.Entities.Enums;

namespace API.DTOs.Settings;

/// <summary>
/// All configuration regarding OIDC
/// </summary>
/// <remarks>This class is saved as a JsonObject in the DB, assign default values to prevent unexpected NPE</remarks>
public sealed record OidcConfigDto: OidcPublicConfigDto
{
    /// <summary>
    /// Optional OpenID Connect Authority URL. Not managed in DB. Managed in appsettings.json and synced to DB.
    /// </summary>
    public string Authority { get; set; } = string.Empty;
    /// <summary>
    /// Optional OpenID Connect ClientId, defaults to kavita. Not managed in DB. Managed in appsettings.json and synced to DB.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;
    /// <summary>
    /// Optional OpenID Connect Secret. Not managed in DB. Managed in appsettings.json and synced to DB.
    /// </summary>
    public string Secret { get; set; } = string.Empty;
    /// <summary>
    /// If true, auto creates a new account when someone logs in via OpenID Connect
    /// </summary>
    public bool ProvisionAccounts { get; set; } = false;
    /// <summary>
    /// Require emails to be verified by the OpenID Connect provider when creating accounts on login
    /// </summary>
    public bool RequireVerifiedEmail { get; set; } = true;
    /// <summary>
    /// Overwrite Kavita roles, libraries and age rating with OpenIDConnect provided roles on log in.
    /// </summary>
    public bool SyncUserSettings { get; set; } = false;
    /// <summary>
    /// A prefix that all roles Kavita checks for during sync must have
    /// </summary>
    public string RolesPrefix { get; set; } = string.Empty;
    /// <summary>
    /// The JWT claim roles are mapped under, defaults to <see cref="ClaimTypes.Role"/>
    /// </summary>
    public string RolesClaim { get; set; } = ClaimTypes.Role;
    /// <summary>
    /// Custom scopes Kavita should request from your OIDC provider
    /// </summary>
    /// <remarks>Advanced setting</remarks>
    public List<string> CustomScopes { get; set; } = [];

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
