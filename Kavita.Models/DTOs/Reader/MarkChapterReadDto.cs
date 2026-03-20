namespace Kavita.Models.DTOs.Reader;

public sealed record MarkChapterReadDto
{
    public int SeriesId { get; init; }
    public int ChapterId { get; init; }

    /// <summary>
    /// If true, generates a new reading session for the user. Based on the estimated time from the current progress
    /// till the end
    /// </summary>
    public bool GenerateReadingSession { get; init; }
}
