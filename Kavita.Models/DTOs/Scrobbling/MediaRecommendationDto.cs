using System.Collections.Generic;
using Kavita.Models.DTOs.KavitaPlus.ExternalMetadata;
using Kavita.Models.Entities.Enums;

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
    public ScrobbleProvider Provider { get; set; }
}
