using System.Collections.Generic;
using API.Services.Plus;
using Microsoft.EntityFrameworkCore;


namespace API.Entities.Metadata;

[Index(nameof(SeriesId), IsUnique = false)]
public class ExternalRecommendation
{
    public int Id { get; set; }

    public required string Name { get; set; }
    public required string CoverUrl { get; set; }
    public required string Url { get; set; }
    public string? Summary { get; set; }
    public int? AniListId { get; set; }
    public long? MalId { get; set; }
    public ScrobbleProvider Provider { get; set; } = ScrobbleProvider.AniList;

    /// <summary>
    /// When null, represents an external series. When set, it is a Series
    /// </summary>
    public int? SeriesId { get; set; }
    //public virtual Series? Series { get; set; }

    // Relationships
    public ICollection<ExternalSeriesMetadata> ExternalSeriesMetadatas { get; set; } = null!;
}
