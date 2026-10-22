namespace Kavita.Models.DTOs.SignalR;

public enum FolderWatchFallbackReason
{
    /// <summary>
    /// The folder's watcher raised 3 errors within 10 minutes
    /// </summary>
    Errors = 0,
    /// <summary>
    /// A scan found a change in the folder that the watcher never reported
    /// </summary>
    MissedChange = 1,
}
