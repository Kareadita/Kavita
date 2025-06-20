namespace API.DTOs.Metadata.Browse;

public sealed record BrowseTagDto : TagDto
{
    /// <summary>
    /// Number of Series this Entity is on
    /// </summary>
    public int SeriesCount { get; set; }
    /// <summary>
    /// Number of Chapters this Entity is on
    /// </summary>
    public int ChapterCount { get; set; }
}
