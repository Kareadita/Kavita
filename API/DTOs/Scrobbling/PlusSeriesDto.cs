namespace API.DTOs.Scrobbling;

public record PlusSeriesDto
{
    public int? AniListId { get; set; }
    public long? MalId { get; set; }
    public string? GoogleBooksId { get; set; }
    public string? MangaDexId { get; set; }
    public string SeriesName { get; set; }
    public string? AltSeriesName { get; set; }
    public MediaFormat MediaFormat { get; set; }
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
