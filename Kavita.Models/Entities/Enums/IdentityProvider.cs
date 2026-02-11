using System.ComponentModel;

namespace Kavita.Models.Entities.Enums;

/// <summary>
/// Who provides the identity of the user
/// </summary>
public enum IdentityProvider
{
    [Description("Kavita")]
    Kavita = 0,
    [Description("OpenID Connect")]
    OpenIdConnect = 1,
}
