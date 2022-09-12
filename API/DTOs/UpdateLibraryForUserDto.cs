using System.Collections.Generic;

namespace API.DTOs;

public class UpdateLibraryForUserDto
{
    public string Username { get; init; }
    public IEnumerable<LibraryDto> SelectedLibraries { get; init; }
}
