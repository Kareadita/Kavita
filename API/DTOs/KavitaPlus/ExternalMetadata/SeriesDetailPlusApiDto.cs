using System.Collections.Generic;
using API.DTOs.KavitaPlus.Metadata;
using API.DTOs.Recommendation;
using API.DTOs.Scrobbling;
using API.DTOs.SeriesDetail;

namespace API.DTOs.KavitaPlus.ExternalMetadata;

public sealed record SeriesDetailPlusApiDto
{
    public IEnumerable<MediaRecommendationDto> Recommendations { get; set; }
    public IEnumerable<UserReviewDto> Reviews { get; set; }
    public IEnumerable<RatingDto> Ratings { get; set; }
    public ExternalSeriesDetailDto? Series { get; set; }
    public int? AniListId { get; set; }
    public long? MalId { get; set; }
    public int? CbrId { get; set; }
}
