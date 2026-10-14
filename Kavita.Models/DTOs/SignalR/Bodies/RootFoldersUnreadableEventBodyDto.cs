using System.Collections.Generic;

namespace Kavita.Models.DTOs.SignalR.Bodies;

/// <param name="Folders">The library roots that could not be listed</param>
public sealed record RootFoldersUnreadableEventBodyDto(string Name, string Title, string SubTitle,
    int LibraryId, string LibraryName, IList<string> Folders);
