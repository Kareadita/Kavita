using System.Collections.Generic;

namespace API.DTOs;

public class UpdateLibraryForUserDto
{
    public required string Username { get; init; }
    public required IEnumerable<LibraryDto> SelectedLibraries { get; init; } = new List<LibraryDto>();
}
