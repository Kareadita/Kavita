using System.Collections.Generic;
using API.Entities.Enums;

namespace API.DTOs;

public class UpdateLibraryDto
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public LibraryType Type { get; set; }
    public required IEnumerable<string> Folders { get; init; }
    public bool FolderWatching { get; init; }
    public bool IncludeInDashboard { get; init; }
    public bool IncludeInRecommended { get; init; }
    public bool IncludeInSearch { get; init; }

}
