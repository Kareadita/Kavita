using System.Collections.Generic;

namespace API.Entities
{
    public class Volume
    {
        public int Id { get; set; }
        public string Number { get; set; }
        public ICollection<MangaFile> Files { get; set; }

        // Many-to-Many relationships
        public Series Series { get; set; }
        public int SeriesId { get; set; }
    }
}