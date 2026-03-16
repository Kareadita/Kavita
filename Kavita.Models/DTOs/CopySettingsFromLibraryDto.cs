using System.Collections.Generic;

namespace Kavita.Models.DTOs;

public sealed record CopySettingsFromLibraryDto
{
    public int SourceLibraryId { get; set; }
    public List<int> TargetLibraryIds { get; set; }
    /// <summary>
    /// Include copying over the type
    /// </summary>
    public bool IncludeType { get; set; }

}
