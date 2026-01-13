using System.Collections.Generic;
using API.Entities.Enums;

namespace API.DTOs.Misc;

public sealed record ParseBulkRequestDto
{
    public ICollection<string> Names { get; set; }
    public LibraryType LibraryType { get; set; }
}
