using API.Services.Plus;

namespace API.DTOs;

public class RatingDto
{
    public int AverageScore { get; set; }
    public int MeanScore { get; set; }
    public int FavoriteCount { get; set; }
    public ScrobbleProvider Provider { get; set; }
}
