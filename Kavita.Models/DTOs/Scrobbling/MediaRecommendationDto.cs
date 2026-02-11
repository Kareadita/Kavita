using System.Collections.Generic;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs.Scrobbling;
#nullable enable

public sealed record MediaRecommendationDto
{
    public int Rating { get; set; }
    public IEnumerable<string> RecommendationNames { get; set; } = null!;
    public string Name { get; set; }
    public string CoverUrl { get; set; }
    public string SiteUrl { get; set; }
    public string? Summary { get; set; }
    public int? AniListId { get; set; }
    public long? MalId { get; set; }
    public ScrobbleProvider Provider { get; set; }
}
