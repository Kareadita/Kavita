#nullable enable
using API.Entities.Enums;

namespace API.DTOs.Settings;

public record OidcPublicConfigDto
{
    /// <inheritdoc cref="ServerSettingKey.OidcAuthority"/>
    public string? Authority { get; set; }
    /// <inheritdoc cref="ServerSettingKey.OidcClientId"/>
    public string? ClientId { get; set; }
    /// <summary>
    /// Optional OpenID Connect ClientSecret, required if authority is set
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
}
