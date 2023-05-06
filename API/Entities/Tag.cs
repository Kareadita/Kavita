using System.Collections.Generic;
using API.Entities.Metadata;
using Microsoft.EntityFrameworkCore;

namespace API.Entities;

[Index(nameof(NormalizedTitle), IsUnique = true)]
public class Tag
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string NormalizedTitle { get; set; }

    public ICollection<SeriesMetadata> SeriesMetadatas { get; set; } = null!;
    public ICollection<Chapter> Chapters { get; set; } = null!;
}
