using System.Collections.Generic;

namespace API.DTOs
{
    // NOTE: Should this be a Record? https://www.youtube.com/watch?v=9Byvwa9yF-I
    public class UpdateLibraryDto
    {
        public string Username { get; init; }
        public IEnumerable<LibraryDto> SelectedLibraries { get; init; }
    }
}