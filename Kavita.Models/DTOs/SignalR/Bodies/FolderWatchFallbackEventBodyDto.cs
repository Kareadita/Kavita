namespace Kavita.Models.DTOs.SignalR.Bodies;

/// <param name="Folder">The folder whose watcher failed or missed a change</param>
public sealed record FolderWatchFallbackEventBodyDto(string Name, string Title, string SubTitle,
    int LibraryId, string LibraryName, string Folder, FolderWatchFallbackReason Reason);
