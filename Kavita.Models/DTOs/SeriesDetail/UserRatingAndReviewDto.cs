namespace Kavita.Models.DTOs.SeriesDetail;

/// <summary>
/// Exclusively for a rate and review modal after finishing a manga series
/// </summary>
public sealed record UserRatingAndReviewDto
{
    public float Rating { get; set; }
    public string Review { get; set; } = string.Empty;
    public bool HasBeenRated { get; set; }
}
