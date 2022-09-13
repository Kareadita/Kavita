using System.Collections.Generic;
using API.Entities.Enums;

namespace API.DTOs;

public class UpdateLibraryDto
{
    public int Id { get; init; }
    public string Name { get; init; }
    public LibraryType Type { get; set; }
    public IEnumerable<string> Folders { get; init; }
}
