using System.Collections.Generic;
using API.Services.Plus;

namespace API.Entities.Metadata;

public class ExternalRating
{
    public int Id { get; set; }

    public int AverageScore { get; set; }
    public int FavoriteCount { get; set; }
    public ScrobbleProvider Provider { get; set; }
    public string? ProviderUrl { get; set; }
    public int SeriesId { get; set; }

    public ICollection<ExternalSeriesMetadata> ExternalSeriesMetadatas { get; set; } = null!;
}
