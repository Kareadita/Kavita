using System.Collections.Generic;

namespace Kavita.Models.DTOs.SignalR.Bodies;

/// <param name="Folders">The first <see cref="MaxFolders"/> folders, <see cref="FolderCount"/> has the total</param>
public sealed record UnreadableFoldersEventBodyDto(string Name, string Title, string SubTitle,
    int LibraryId, string LibraryName, IList<string> Folders, int FolderCount)
{
    public const int MaxFolders = 10;
}
