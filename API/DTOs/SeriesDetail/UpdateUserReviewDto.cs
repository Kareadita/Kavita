using System.ComponentModel.DataAnnotations;

namespace API.DTOs.SeriesDetail;

public class UpdateUserReviewDto
{
    public int SeriesId { get; set; }
    [MaxLength(120)]
    public string? Tagline { get; set; }
    [MaxLength(1000)]
    public string Body { get; set; }
}
