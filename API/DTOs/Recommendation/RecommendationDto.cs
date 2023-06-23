using System.Collections.Generic;

namespace API.DTOs.Recommendation;

public class RecommendationDto
{
    public IList<SeriesDto> OwnedSeries { get; set; } = new List<SeriesDto>();
    public IList<ExternalSeriesDto> ExternalSeries { get; set; } = new List<ExternalSeriesDto>();
}
