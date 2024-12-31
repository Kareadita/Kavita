using API.DTOs.Recommendation;

namespace API.DTOs.Metadata.Matching;

public class ExternalSeriesMatchDto
{
    public ExternalSeriesDetailDto Series { get; set; }
    public float MatchRating { get; set; }
}
