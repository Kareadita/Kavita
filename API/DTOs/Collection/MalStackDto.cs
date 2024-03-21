namespace API.DTOs.Collection;

/// <summary>
/// Represents an Interest Stack from MAL
/// </summary>
public class MalStackDto
{
    public required string Title { get; set; }
    public required long StackId { get; set; }
    public required string Url { get; set; }
    public required string? Author { get; set; }
    public required int SeriesCount { get; set; }
    public required int RestackCount { get; set; }
}
