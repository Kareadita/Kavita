using Kavita.Models.Entities.Enums.KavitaPlus;

namespace Kavita.Models.DTOs.Recommendation;
#nullable enable

/// <summary>
/// An owned (in-library) series surfaced as a recommendation, tagged with why it was recommended
/// </summary>
public sealed record RecommendedSeriesDto
{
    public required SeriesDto Series { get; set; }
    /// <summary>
    /// Why this series was recommended
    /// </summary>
    public RecommendationSource Source { get; set; }
}
