using System.Collections.Generic;

namespace Kavita.Models.DTOs;

public sealed record CheckForFilesInFolderRootsDto
{
    public ICollection<string> Roots { get; init; }
}
