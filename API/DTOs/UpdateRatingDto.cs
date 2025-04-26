namespace API.DTOs;

public class UpdateRatingDto
{
    public int SeriesId { get; init; }
    public int? ChapterId { get; init; }
    public float UserRating { get; init; }
}
