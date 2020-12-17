using System.Collections.Generic;
using API.Entities;

namespace API.DTOs
{
    public class LibraryDto
    {
        public string Name { get; set; }
        public string CoverImage { get; set; }
        public LibraryType Type { get; set; }
        public ICollection<string> Folders { get; set; }
    }
}