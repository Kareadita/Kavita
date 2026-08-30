

using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.KavitaPlus;
using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs.Recommendation;
#nullable enable

public sealed record ExternalSeriesDto
{
    public required string Name { get; set; }
    public required string CoverUrl { get; set; }
    public required string Url { get; set; }
    public string? Summary { get; set; }
    public int? AniListId { get; set; }
    public int? MangaBakaId { get; set; }
    public long? MalId { get; set; }
    public int? HardcoverId { get; set; }
    [EnumDataType(typeof(ScrobbleProvider))]
    public ScrobbleProvider Provider { get; set; } = ScrobbleProvider.AniList;
    /// <summary>
    /// Provider this recommendation came from (replaces <see cref="Provider"/> going forward).
    /// </summary>
    [EnumDataType(typeof(MetadataProvider))]
    public MetadataProvider MetadataProvider { get; set; }
    /// <summary>
    /// Why this series was recommended (Similar vs Personalized), surfaced as a badge in the UI.
    /// </summary>
    [EnumDataType(typeof(RecommendationSource))]
    public RecommendationSource RecommendationSource { get; set; }

    /// <summary>
    /// The effective age rating for this recommendation, used to filter it against the requesting user's
    /// age restriction. Unknown/indeterminate ratings are stored as the most restrictive value (fail closed).
    /// </summary>
    [EnumDataType(typeof(AgeRating))]
    public AgeRating AgeRating { get; set; } = AgeRating.Unknown;
}
