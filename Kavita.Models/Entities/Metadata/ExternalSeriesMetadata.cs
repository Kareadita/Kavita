using System;
using System.Collections.Generic;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Interfaces;

namespace Kavita.Models.Entities.Metadata;

/// <summary>
/// External Metadata from Kavita+ for a Series
/// </summary>
public class ExternalSeriesMetadata : IEntityDate
{
    public int Id { get; set; }

    /// <summary>
    /// Where is this metadata coming from
    /// </summary>
    public MetadataProvider Provider { get; set; }
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
    public int CbrId { get; set; }
    public long MalId { get; set; }
    public string GoogleBooksId { get; set; }
    public long MangabakaId { get; set; }
    public int HardcoverId { get; set; }

    /// <summary>
    /// Data is valid until this time
    /// </summary>
    public DateTime ValidUntilUtc { get; set; }

    public Series Series { get; set; } = null!;
    public int SeriesId { get; set; }
    public DateTime Created { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime LastModifiedUtc { get; set; }
}
