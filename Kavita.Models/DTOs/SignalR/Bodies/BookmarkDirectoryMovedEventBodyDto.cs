namespace Kavita.Models.DTOs.SignalR.Bodies;
#nullable enable

/// <param name="Moved">Files moved into the new folder</param>
/// <param name="Skipped">Files left in the old folder because the new folder already had a file of that name</param>
public sealed record BookmarkDirectoryMovedEventBodyDto(string Name, string Title, string SubTitle, string Path, int Moved,
    int Skipped);
