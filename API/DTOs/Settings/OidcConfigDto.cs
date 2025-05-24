#nullable enable
namespace API.DTOs.Settings;

public class OidcConfigDto
{
    /// <summary>
    /// Base url for authority, must have /.well-known/openid-configuration
    /// </summary>
    public string? Authority { get; set; }

    /// <summary>
    /// ClientId configured in your OpenID Connect provider
    /// </summary>
    public string? ClientId { get; set; }
    /// <summary>
    /// Create a new account when someone logs in with an unmatched account, if <see cref="RequireVerifiedEmail"/> is true,
    /// will account will be verified by default
    /// </summary>
    public bool ProvisionAccounts { get; set; }
    /// <summary>
    /// Require emails from OpenIDConnect to be verified before use
    /// </summary>
    public bool RequireVerifiedEmail { get; set; }
    /// <summary>
    /// Overwrite Kavita roles, libraries and age rating with OpenIDConnect provides roles on log in.
    /// </summary>
    public bool ProvisionUserSettings { get; set; }
    /// <summary>
    /// Try logging in automatically when opening the app
    /// </summary>
    public bool AutoLogin { get; set; }
}
