using System;
using System.Collections.Generic;
using API.Entities.Interfaces;

namespace API.Entities
{
    public class Volume : IEntityDate
    {
        public int Id { get; set; }
        public string Number { get; set; }
        public ICollection<MangaFile> Files { get; set; }
        public DateTime Created { get; set; }
        public DateTime LastModified { get; set; }

        // Many-to-Many relationships
        public Series Series { get; set; }
        public int SeriesId { get; set; }
    }
}