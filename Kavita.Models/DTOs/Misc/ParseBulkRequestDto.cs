using System.Collections.Generic;
using Kavita.Models.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs.Misc;

public sealed record ParseBulkRequestDto
{
    public ICollection<string> Names { get; set; }
    [EnumDataType(typeof(LibraryType))]
    public LibraryType LibraryType { get; set; }
}