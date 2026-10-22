using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs;
#nullable enable

/// <summary>
/// How a library notices changes right now, and why, for the library's Folder tab
/// </summary>
public sealed record FolderWatchStatusDto
{
    public FolderWatchMode Mode { get; init; }
    public FolderWatchMethod Method { get; init; }
    /// <summary>
    /// Set when the watcher failed or missed a change, with <see cref="Folder"/>
    /// </summary>
    public FolderWatchFallbackReason? Reason { get; init; }
    public string? Folder { get; init; }
    /// <summary>
    /// Changes the watcher confirmed during Trial, the lowest among the library's mounts in Trial
    /// </summary>
    public int Successes { get; init; }
    public int SuccessesNeeded { get; init; }
    /// <summary>
    /// Trial also waits for one library scan with no miss
    /// </summary>
    public bool NeedsLibraryScan { get; init; }
    public bool EnableFolderWatching { get; init; }
}
