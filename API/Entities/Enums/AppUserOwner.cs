namespace API.Entities.Enums;

/// <summary>
/// Who own the user, can be updated in the UI if desired
/// </summary>
public enum AppUserOwner
{
    /**
     * Kavita has full control over the user
     */
    Native = 0,
    /**
     * The user is synced with the OIDC provider
     */
    OpenIdConnect = 1,
}
