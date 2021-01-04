using System.Collections.Generic;

namespace API.DTOs
{
    public class UpdateLibraryDto
    {
        public int Id { get; init; }
        public string Name { get; init; }
        public IEnumerable<string> Folders { get; init; }
    }
}