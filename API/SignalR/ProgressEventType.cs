namespace API.SignalR;

public static class ProgressEventType
{
    public const string Started = "started";

    public const string Updated = "updated";
    /// <summary>
    /// End of the update chain
    /// </summary>
    public const string Ended = "ended";
    /// <summary>
    /// Represents a single update
    /// </summary>
    public const string Single = "started";

}
