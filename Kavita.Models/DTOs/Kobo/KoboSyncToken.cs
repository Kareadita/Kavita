namespace Kavita.Models.DTOs.Kobo;

public sealed record KoboSyncToken
{

    /// <summary>
    /// Increment if the Kobo Smart Filter changes
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Sync token with upstream store
    /// </summary>
    public string KoboStoreSyncToken { get; set; }

    /// <summary>
    /// PK of the KoboSyncPoint
    /// </summary>
    public int? SyncPointId { get; set; }


}
