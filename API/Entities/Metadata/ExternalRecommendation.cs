using System.Collections.Generic;
using API.Services.Plus;

namespace API.Entities.Metadata;

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

    public int? SeriesId { get; set; }

    // Relationships
    public ICollection<ExternalSeriesMetadata> ExternalSeriesMetadatas { get; set; } = null!;
}
