using System.ComponentModel.DataAnnotations;

namespace API.DTOs;

public class UpdateSeriesRatingDto
{
    public int SeriesId { get; init; }
    public int UserRating { get; init; }
}
