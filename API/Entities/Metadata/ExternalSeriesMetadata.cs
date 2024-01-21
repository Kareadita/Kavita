using System;
using System.Collections.Generic;

namespace API.Entities.Metadata;

/// <summary>
/// External Metadata from Kavita+ for a Series
/// </summary>
public class ExternalSeriesMetadata
{
    public int Id { get; set; }
    /// <summary>
    /// External Reviews for the Series. Managed by Kavita for Kavita+ users
    /// </summary>
    public ICollection<ExternalReview> ExternalReviews { get; set; } = null!;
    public ICollection<ExternalRating> ExternalRatings { get; set; } = null!;
    public ICollection<ExternalRecommendation> ExternalRecommendations { get; set; } = null!;

    /// <summary>
    /// Average External Rating. -1 means not set
    /// </summary>
    public int AverageExternalRating { get; set; } = 0;

    public int AniListId { get; set; }
    public long MalId { get; set; }
    public string GoogleBooksId { get; set; }

    public DateTime LastUpdatedUtc { get; set; }

    public Series Series { get; set; } = null!;
    public int SeriesId { get; set; }
}
