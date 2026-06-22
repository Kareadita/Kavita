using System;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs.KavitaPlus.Scrobble;

public sealed record ScrobbleProviderDto
{
    public ScrobbleProvider Provider { get; set; }

    /// <summary>
    /// Username on the provider's platform
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Authentication Token for the provider
    /// </summary>
    public string AuthenticationToken { get; set; }

    /// <summary>
    /// Refresh Token for the provider. Not all providers support automatic refreshes
    /// </summary>
    public string RefreshToken { get; set; }

    /// <summary>
    /// Token valid until
    /// </summary>
    public DateTime ValidUntilUtc { get; set; }

    /// <summary>
    /// Last synced information with the provider
    /// </summary>
    public DateTime LastSyncedUtc { get; set; }

    /// <summary>
    /// Has the user ran Scrobble Event Generation
    /// </summary>
    /// <remarks>Only applicable for Kavita+ and when a Token is present</remarks>
    public bool HasRunScrobbleEventGeneration { get; set; }
    /// <summary>
    /// The timestamp of when Scrobble Event Generation ran (Utc)
    /// </summary>
    /// <remarks>Kavita+ only</remarks>
    public DateTime ScrobbleEventGenerationRan { get; set; }

    public ScrobbleProviderSettingsDto Settings { get; set; }
}
