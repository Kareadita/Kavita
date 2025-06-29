#nullable enable
namespace API.DTOs.Settings;

public sealed record OidcPublicConfigDto
{
    /// <inheritdoc cref="OidcConfigDto.Authority"/>
    public string? Authority { get; set; }
    /// <inheritdoc cref="OidcConfigDto.ClientId"/>
    public string? ClientId { get; set; }
    /// <inheritdoc cref="OidcConfigDto.AutoLogin"/>
    public bool AutoLogin { get; set; }
}
