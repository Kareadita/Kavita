using System.Collections.Generic;
using Kavita.Models.Entities.Interfaces;
using Kavita.Models.Entities.Metadata;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Models.Entities;

[Index(nameof(NormalizedTitle), IsUnique = true)]
public class Tag : ITag
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string NormalizedTitle { get; set; } = null!;

    public ICollection<SeriesMetadata> SeriesMetadatas { get; set; } = null!;
    public ICollection<Chapter> Chapters { get; set; } = null!;
}
