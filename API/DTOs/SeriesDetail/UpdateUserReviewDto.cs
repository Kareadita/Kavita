using System.ComponentModel.DataAnnotations;

namespace API.DTOs.SeriesDetail;
#nullable enable

public class UpdateUserReviewDto
{
    public int SeriesId { get; set; }
    public string Body { get; set; }
}
