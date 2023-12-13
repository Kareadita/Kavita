using API.Services.Plus;

namespace API.DTOs;
#nullable enable

public class RatingDto
{
    public int AverageScore { get; set; }
    public int FavoriteCount { get; set; }
    public ScrobbleProvider Provider { get; set; }
    public string? ProviderUrl { get; set; }
}
