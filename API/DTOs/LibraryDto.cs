using System.Collections.Generic;
using API.Entities.Enums;

namespace API.DTOs
{
    public class LibraryDto
    {
        public int Id { get; init; }
        public string Name { get; init; }
        public string CoverImage { get; init; }
        public LibraryType Type { get; init; }
        public ICollection<string> Folders { get; init; }
    }
}