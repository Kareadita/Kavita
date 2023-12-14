using System.ComponentModel.DataAnnotations;

namespace API.DTOs.SeriesDetail;
#nullable enable

public class UpdateUserReviewDto
{
    public int SeriesId { get; set; }
    [MaxLength(120)]
    public string? Tagline { get; set; }
    public string Body { get; set; }
}
