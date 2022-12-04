using System.Collections.Generic;
using API.Entities.Enums;
using API.Entities.Metadata;

namespace API.Entities;

public class Person
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? NormalizedName { get; set; }
    public PersonRole Role { get; set; }

    // Relationships
    public ICollection<SeriesMetadata> SeriesMetadatas { get; set; } = null!;
    public ICollection<Chapter> ChapterMetadatas { get; set; } = null!;
}
