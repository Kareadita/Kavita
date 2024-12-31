using API.Services.Plus;

namespace API.DTOs.Recommendation;
#nullable enable

public class ExternalSeriesDto
{
    public required string Name { get; set; }
    public required string CoverUrl { get; set; }
    public required string Url { get; set; }
    public string? Summary { get; set; }
    public int? AniListId { get; set; }
    public long? MalId { get; set; }
    public ScrobbleProvider Provider { get; set; } = ScrobbleProvider.AniList;


}
