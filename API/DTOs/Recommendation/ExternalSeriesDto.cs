namespace API.DTOs.Recommendation;
#nullable enable

public class ExternalSeriesDto
{
    public required string Name { get; set; }
    public required string CoverUrl { get; set; }
    public required string Url { get; set; }
    public string? Summary { get; set; }
}
