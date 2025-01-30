namespace API.DTOs.Scrobbling;

/// <summary>
/// Represents information about a potential Series for Kavita+
/// </summary>
public record PlusSeriesRequestDto
{
    public int? AniListId { get; set; }
    public long? MalId { get; set; }
    public string? GoogleBooksId { get; set; }
    public string? MangaDexId { get; set; }
    public string SeriesName { get; set; }
    public string? AltSeriesName { get; set; }
    public PlusMediaFormat MediaFormat { get; set; }
    /// <summary>
    /// Optional but can help with matching
    /// </summary>
    public int? ChapterCount { get; set; }
    /// <summary>
    /// Optional but can help with matching
    /// </summary>
    public int? VolumeCount { get; set; }
    public int? Year { get; set; }
}
