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
    /// <summary>
    /// External recommendations will include all recommendations and will have a seriesId if it's on this Kavita instance.
    /// </summary>
    /// <remarks>Cleanup Service will perform matching to tie new series with recommendations</remarks>
    public ICollection<ExternalRecommendation> ExternalRecommendations { get; set; } = null!;

    /// <summary>
    /// Average External Rating. -1 means not set, 0 - 100
    /// </summary>
    public int AverageExternalRating { get; set; } = -1;

    public int AniListId { get; set; }
    public long MalId { get; set; }
    public string GoogleBooksId { get; set; }

    /// <summary>
    /// Data is valid until this time
    /// </summary>
    public DateTime ValidUntilUtc { get; set; }

    public Series Series { get; set; } = null!;
    public int SeriesId { get; set; }
}
