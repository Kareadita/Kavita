using System.Collections.Generic;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs.Misc;

public sealed record ParseBulkRequestDto
{
    public ICollection<string> Names { get; set; }
    public LibraryType LibraryType { get; set; }
}
