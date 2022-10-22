using System.Collections.Generic;
using API.Entities.Enums;
using API.Entities.Metadata;

namespace API.Entities;

public enum ProviderSource
{
    Local = 1,
    External = 2
}
public class Person
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string NormalizedName { get; set; }
    public PersonRole Role { get; set; }
    //public ProviderSource Source { get; set; }

    // Relationships
    public ICollection<SeriesMetadata> SeriesMetadatas { get; set; }
    public ICollection<Chapter> ChapterMetadatas { get; set; }
}
