#nullable enable

namespace API.DTOs.Settings;

public record OidcConfigDto: OidcPublicConfigDto
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
    /// Overwrite Kavita roles, libraries and age rating with OpenIDConnect provides roles on log in.
    /// </summary>
    public bool SyncUserSettings { get; set; }

    /// <summary>
    /// Returns true if the <see cref="OidcPublicConfigDto.Authority"/> has been set
    /// </summary>
    public bool Enabled => Authority != "";
}
