namespace API.SignalR;

/// <summary>
/// How progress should be represented on the UI
/// </summary>
public static class ProgressType
{
    /// <summary>
    /// Progress scales from 0F -> 1F
    /// </summary>
    public const string Determinate = "determinate";
    /// <summary>
    /// Progress has no understanding of quantity
    /// </summary>
    public const string Indeterminate = "indeterminate";
    /// <summary>
    /// No progress component to the event
    /// </summary>
    public const string None = "";

}
