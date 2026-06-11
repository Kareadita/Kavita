using System.Collections.Generic;
using Kavita.Models.DTOs.KavitaPlus.Metadata;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.DTOs.SeriesDetail;

namespace Kavita.Models.DTOs.KavitaPlus.ExternalMetadata;
#nullable enable

public sealed record SeriesDetailPlusApiDto
{
    public IEnumerable<MediaRecommendationDto> Recommendations { get; set; }
    public IEnumerable<UserReviewDto> Reviews { get; set; }
    public IEnumerable<RatingDto> Ratings { get; set; }
    public ExternalSeriesDetailDto? Series { get; set; }
    public int? AniListId { get; set; }
    public long? MalId { get; set; }
    public int? MangabakaId { get; set; }
    public int? HardCoverId { get; set; }
    public int? CbrId { get; set; }
}
