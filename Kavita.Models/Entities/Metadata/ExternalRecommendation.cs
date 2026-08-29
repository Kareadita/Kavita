using System;
using System.Collections.Generic;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.KavitaPlus;
using Microsoft.EntityFrameworkCore;


namespace Kavita.Models.Entities.Metadata;

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
    public int? MangaBakaId { get; set; }
    /// <remarks>This ALWAYS points towards a book. Hardcover does not have series recommendations</remarks>
    public int? HardCoverId { get; set; }
    public MetadataProvider MetadataProvider { get; set; }
    [Obsolete("Use MetadataProvider instead")]
    public ScrobbleProvider Provider { get; set; } = ScrobbleProvider.AniList;

    public RecommendationSource RecommendationSource { get; set; }

    /// <summary>
    /// The effective age rating for this recommendation (provider base rating raised by Kavita's tag/genre
    /// age-rating mappings). Used to filter external recommendations against a user's age restriction.
    /// </summary>
    /// <remarks>Only meaningful for external recs (<see cref="SeriesId"/> null); owned recs are filtered
    /// against their real Series metadata instead. Unknown/indeterminate ratings are stored as the most
    /// restrictive value so they fail closed for restricted users.</remarks>
    public AgeRating AgeRating { get; set; } = AgeRating.Unknown;

    /// <summary>
    /// When null, represents an external series. When set, it is a Series
    /// </summary>
    public int? SeriesId { get; set; }

    // Relationships
    public ICollection<ExternalSeriesMetadata> ExternalSeriesMetadatas { get; set; } = null!;
}
