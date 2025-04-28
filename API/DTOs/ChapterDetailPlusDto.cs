#nullable enable
using System.Collections.Generic;
using API.DTOs.SeriesDetail;

namespace API.DTOs;

public class ChapterDetailPlusDto
{
    public float Rating { get; set; }
    public bool HasBeenRated { get; set; }

    public List<UserReviewDto> Reviews { get; set; }
    public List<RatingDto>? Ratings { get; set; }
}
