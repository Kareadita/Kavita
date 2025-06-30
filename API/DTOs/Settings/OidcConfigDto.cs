#nullable enable
using API.Entities.Enums;

namespace API.DTOs.Settings;

public class OidcConfigDto
{
    /// <inheritdoc cref="ServerSettingKey.OidcAuthority"/>
    public string? Authority { get; set; }
    /// <inheritdoc cref="ServerSettingKey.OidcClientId"/>
    public string? ClientId { get; set; }
    /// <inheritdoc cref="ServerSettingKey.OidcProvisionAccounts"/>
    public bool ProvisionAccounts { get; set; }
    /// <inheritdoc cref="ServerSettingKey.OidcRequireVerifiedEmail"/>
    public bool RequireVerifiedEmail { get; set; }
    /// <inheritdoc cref="ServerSettingKey.OidcProvisionUserSettings"/>
    public bool ProvisionUserSettings { get; set; }
    /// <inheritdoc cref="ServerSettingKey.OidcAutoLogin"/>
    public bool AutoLogin { get; set; }
    /// <inheritdoc cref="ServerSettingKey.DisablePasswordAuthentication"/>
    public bool DisablePasswordAuthentication { get; set; }

    /// <summary>
    /// Returns true if the <see cref="Authority"/> has been set
    /// </summary>
    public bool Enabled => Authority != "";
}
