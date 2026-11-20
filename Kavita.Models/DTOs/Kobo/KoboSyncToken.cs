namespace Kavita.Models.DTOs.Kobo;

public sealed record KoboSyncToken
{

    /// <summary>
    /// Increment if the Kobo Smart Filter changes
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// PK of the KoboSyncPoint
    /// </summary>
    public int? SyncPointId { get; set; }

}
