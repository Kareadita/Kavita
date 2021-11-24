using System.Collections.Generic;
using API.Entities.Enums;

namespace API.Entities
{
    public class Person
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string NormalizedName { get; set; }
        public PersonRole Role { get; set; }

        // Relationships
        public ICollection<SeriesMetadata> SeriesMetadatas { get; set; }
        public ICollection<ChapterMetadata> ChapterMetadatas { get; set; }
    }
}
