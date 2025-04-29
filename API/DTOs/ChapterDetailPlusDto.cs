#nullable enable
using System.Collections.Generic;
using API.DTOs.SeriesDetail;

namespace API.DTOs;

public class ChapterDetailPlusDto
{
    public float Rating { get; set; }
    public bool HasBeenRated { get; set; }

    public IList<UserReviewDto> Reviews { get; set; } = [];
    public IList<RatingDto> Ratings { get; set; } = [];
}
