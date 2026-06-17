using System;
using Kavita.Models.DTOs.KavitaPlus.Scrobble;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.Entities.User;

public class AppUserScrobbleProvider
{

    public ScrobbleProvider Provider { get; set; }

    /// <summary>
    /// Username on the provider's platform
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Authentication Token for the provider
    /// </summary>
    public string AuthenticationToken { get; set; } = string.Empty;

    /// <summary>
    /// Refresh Token for the provider, not used for all providers
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Token valid until
    /// </summary>
    public DateTime ValidUntilUtc { get; set; }

    /// <summary>
    /// Last synced information with the provider
    /// </summary>
    public DateTime LastSyncedUtc { get; set; }

    /// <summary>
    /// The timestamp of when Scrobble Event Generation ran (Utc)
    /// </summary>
    /// <remarks>Kavita+ only</remarks>
    public DateTime ScrobbleEventGenerationRan { get; set; }

    public ScrobbleProviderSettingsDto Settings { get; set; } = new();
}
