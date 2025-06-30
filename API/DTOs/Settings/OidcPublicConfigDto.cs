#nullable enable
using API.Entities.Enums;

namespace API.DTOs.Settings;

public record OidcPublicConfigDto
{
    /// <inheritdoc cref="ServerSettingKey.OidcAuthority"/>
    public string? Authority { get; set; }
    /// <inheritdoc cref="ServerSettingKey.OidcClientId"/>
    public string? ClientId { get; set; }
    /// <inheritdoc cref="ServerSettingKey.OidcAutoLogin"/>
    public bool AutoLogin { get; set; }
    /// <inheritdoc cref="ServerSettingKey.DisablePasswordAuthentication"/>
    public bool DisablePasswordAuthentication { get; set; }
    /// <inheritdoc cref="ServerSettingKey.OidcProviderName"/>
    public string ProviderName { get; set; } = string.Empty;
}
