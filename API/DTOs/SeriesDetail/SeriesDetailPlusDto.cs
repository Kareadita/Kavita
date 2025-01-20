using System.Collections.Generic;
using API.DTOs.Recommendation;

namespace API.DTOs.SeriesDetail;

/// <summary>
/// All the data from Kavita+ for Series Detail
/// </summary>
/// <remarks>This is what the UI sees, not what the API sends back</remarks>
public class SeriesDetailPlusDto
{
    public RecommendationDto? Recommendations { get; set; }
    public IEnumerable<UserReviewDto> Reviews { get; set; }
    public IEnumerable<RatingDto>? Ratings { get; set; }
    public ExternalSeriesDetailDto? Series { get; set; }
}
