using System.Collections.Generic;

namespace Kavita.Models.DTOs.Recommendation;

public sealed record RecommendationDto
{
    public IList<SeriesDto> OwnedSeries { get; set; } = new List<SeriesDto>();
    public IList<ExternalSeriesDto> ExternalSeries { get; set; } = new List<ExternalSeriesDto>();
}
