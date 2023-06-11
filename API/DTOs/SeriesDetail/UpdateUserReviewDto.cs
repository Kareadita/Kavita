using System.ComponentModel.DataAnnotations;

namespace API.DTOs.SeriesDetail;

public class UpdateUserReviewDto
{
    public int SeriesId { get; set; }
    [MinLength(20)]
    [MaxLength(120)]
    public string? Tagline { get; set; }
    [MinLength(20)]
    [MaxLength(1000)]
    public string Body { get; set; }
}
