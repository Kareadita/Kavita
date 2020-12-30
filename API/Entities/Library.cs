using System.Collections.Generic;

namespace API.Entities
{
    public class Library
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string CoverImage { get; set; }
        public LibraryType Type { get; set; }
        public ICollection<FolderPath> Folders { get; set; }
        public ICollection<AppUser> AppUsers { get; set; }
        public ICollection<Series> Series { get; set; }
    }
}