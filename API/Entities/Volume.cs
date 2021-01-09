using System;
using System.Collections.Generic;
using API.Entities.Interfaces;

namespace API.Entities
{
    public class Volume : IEntityDate
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Number { get; set; }
        public ICollection<MangaFile> Files { get; set; }
        public DateTime Created { get; set; }
        public DateTime LastModified { get; set; }
        public byte[] CoverImage { get; set; }
        
        // public string CachePath {get; set;} // Path where cache is located. Default null, resets to null on deletion.
        //public ICollection<AppUserProgress> AppUserProgress { get; set; }

        // Many-to-One relationships
        public Series Series { get; set; }
        public int SeriesId { get; set; }
    }
}