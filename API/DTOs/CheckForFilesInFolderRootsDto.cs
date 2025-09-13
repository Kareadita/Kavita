using System.Collections.Generic;

namespace API.DTOs;

public sealed record CheckForFilesInFolderRootsDto
{
    public ICollection<string> Roots { get; init; }
}
