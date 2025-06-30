#nullable enable
using API.Entities.Enums;

namespace API.DTOs.Settings;

public record OidcConfigDto: OidcPublicConfigDto
{
    /// <inheritdoc cref="ServerSettingKey.OidcProvisionAccounts"/>
    public bool ProvisionAccounts { get; set; }
    /// <inheritdoc cref="ServerSettingKey.OidcRequireVerifiedEmail"/>
    public bool RequireVerifiedEmail { get; set; }
    /// <inheritdoc cref="ServerSettingKey.OidcProvisionUserSettings"/>
    public bool ProvisionUserSettings { get; set; }
    /// <inheritdoc cref="ServerSettingKey.OidcAutoLogin"/>

    /// <summary>
    /// Returns true if the <see cref="OidcPublicConfigDto.Authority"/> has been set
    /// </summary>
    public bool Enabled => Authority != "";
}
