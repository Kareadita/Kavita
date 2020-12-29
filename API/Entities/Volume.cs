using System.Collections.Generic;

namespace API.Entities
{
    public class Volume
    {
        public string Number { get; set; }
        public ICollection<string> Files { get; set; }
        
        // Many-to-Many relationships
        public Series Series { get; set; }
        public int SeriesId { get; set; }
    }
}