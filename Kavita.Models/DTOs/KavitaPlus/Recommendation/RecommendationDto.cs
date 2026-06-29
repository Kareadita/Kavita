using System.Collections.Generic;

namespace Kavita.Models.DTOs.Recommendation;

public sealed record RecommendationDto
{
    /// <summary>
    /// Series in the user's library that surfaced as recommendations, each tagged with its source.
    /// </summary>
    public IList<RecommendedSeriesDto> OwnedSeries { get; set; } = new List<RecommendedSeriesDto>();
    public IList<ExternalSeriesDto> ExternalSeries { get; set; } = new List<ExternalSeriesDto>();
}
