using System.Collections.Generic;
using API.Entities.Enums;
using API.Services.Plus;

namespace API.Entities.Metadata;

public class ExternalRating
{
    public int Id { get; set; }

    public int AverageScore { get; set; }
    public int FavoriteCount { get; set; }
    public ScrobbleProvider Provider { get; set; }
    /// <summary>
    /// Where this rating comes from: Critic or User
    /// </summary>
    public RatingAuthority Authority { get; set; } = RatingAuthority.User;
    public string? ProviderUrl { get; set; }
    public int SeriesId { get; set; }
    /// <summary>
    /// This can be null when for a series-rating
    /// </summary>
    public int? ChapterId { get; set; }

    public ICollection<ExternalSeriesMetadata> ExternalSeriesMetadatas { get; set; } = null!;
}
