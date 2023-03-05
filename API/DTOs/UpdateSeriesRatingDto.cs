using System.ComponentModel.DataAnnotations;

namespace API.DTOs;

public class UpdateSeriesRatingDto
{
    public int SeriesId { get; init; }
    public int UserRating { get; init; }
    [MaxLength(1000)]
    public string? UserReview { get; init; }
}
