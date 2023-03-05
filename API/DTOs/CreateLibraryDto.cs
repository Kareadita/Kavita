using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using API.Entities.Enums;

namespace API.DTOs;

public class CreateLibraryDto
{
    [Required]
    public string Name { get; init; } = default!;
    [Required]
    public LibraryType Type { get; init; }
    [Required]
    [MinLength(1)]
    public IEnumerable<string> Folders { get; init; } = default!;
}
