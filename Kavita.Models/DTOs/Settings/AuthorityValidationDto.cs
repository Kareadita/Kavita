using System.ComponentModel;

namespace Kavita.Models.DTOs.Settings;

public sealed record AuthorityValidationDto(string Authority);

public enum AuthorityValidationResult
{
    /// <summary>
    /// Kavita can load the OIDC configuration and the issuer matches
    /// </summary>
    [Description("Success")]
    Success = 0,
    /// <summary>
    /// Kavita can load the OIDC configuration, but the issuer does not match
    /// </summary>
    [Description("InvalidAuthority")]
    InvalidAuthority = 1,
    /// <summary>
    /// Kavita cannot load the OIDC configuration
    /// </summary>
    [Description("Failure")]
    Failure = 2,
    /// <summary>
    /// Kavita cannot validate the authority because it is not configured
    /// </summary>
    [Description("NotApplicable")]
    NotApplicable = 3,
    /// <summary>
    /// The authority is missing https
    /// </summary>
    [Description("MissingHttps")]
    MissingHttps = 4,
}
