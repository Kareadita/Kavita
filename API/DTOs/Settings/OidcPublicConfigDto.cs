#nullable enable

namespace API.DTOs.Settings;

/**
 * The part of the OIDC configuration that is returned by the API without authentication
 */
public record OidcPublicConfigDto
{
    /// <summary>
    /// Automatically redirect to the Oidc login screen
    /// </summary>
    public bool AutoLogin { get; set; }
    /// <summary>
    /// Disables password authentication for non-admin users
    /// </summary>
    public bool DisablePasswordAuthentication { get; set; }
    /// <summary>
    /// Name of your provider, used to display on the login screen
    /// </summary>
    /// <remarks>Default to OpenID Connect</remarks>
    public string ProviderName { get; set; } = "OpenID Connect";
    public bool Enabled { get; set; } = false;
}
