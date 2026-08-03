using System.Collections.Generic;
using Kavita.Models.DTOs.KavitaPlus.ExternalMetadata;
using Kavita.Models.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs.Scrobbling;
#nullable enable

public sealed record MediaRecommendationDto: MetadataRequest
{
    public int Rating { get; set; }
    public IEnumerable<string> RecommendationNames { get; set; } = null!;
    public string Name { get; set; }
    public string CoverUrl { get; set; }
    public string SiteUrl { get; set; }
    public string? Summary { get; set; }
    /// <summary>
    /// Provider-specific relevance score. For MangaBaka: shared-user count (readers-also-like)
    /// or shared-tag total (similar). Higher is more relevant.
    /// </summary>
    public double? Score { get; set; }
    [EnumDataType(typeof(AgeRating))]
    public AgeRating AgeRating { get; set; } = AgeRating.Unknown;
    public IList<string> Genres { get; set; } = new List<string>();
    public IList<string> Tags { get; set; } = new List<string>();
    [EnumDataType(typeof(ScrobbleProvider))]
    public ScrobbleProvider Provider { get; set; }
}