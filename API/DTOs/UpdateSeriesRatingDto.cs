namespace API.DTOs;

public class UpdateSeriesRatingDto
{
    public int SeriesId { get; init; }
    public float UserRating { get; init; }
}
