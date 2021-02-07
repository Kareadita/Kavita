using System;
using System.Collections.Generic;
using API.Entities.Enums;
using API.Entities.Interfaces;

namespace API.Entities
{
    public class Library : IEntityDate
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string CoverImage { get; set; }
        public LibraryType Type { get; set; }
        public DateTime Created { get; set; }
        public DateTime LastModified { get; set; }
        public ICollection<FolderPath> Folders { get; set; }
        public ICollection<AppUser> AppUsers { get; set; }
        public ICollection<Series> Series { get; set; }
        
    }
}