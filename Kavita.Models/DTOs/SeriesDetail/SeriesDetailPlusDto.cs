using System.Collections.Generic;
using Kavita.Models.DTOs.KavitaPlus.Metadata;
using Kavita.Models.DTOs.Recommendation;

namespace Kavita.Models.DTOs.SeriesDetail;
#nullable enable

/// <summary>
/// All the data from Kavita+ for Series Detail
/// </summary>
/// <remarks>This is what the UI sees, not what the API sends back</remarks>
public sealed record SeriesDetailPlusDto
{
    public RecommendationDto? Recommendations { get; set; }
    public IEnumerable<UserReviewDto> Reviews { get; set; }
    public IEnumerable<RatingDto>? Ratings { get; set; }
    public ExternalSeriesDetailDto? Series { get; set; }
}
