namespace Kavita.Models.DTOs.Reader;

public sealed record MarkReadDto
{
    public int SeriesId { get; init; }
    /// <summary>
    /// If true, generates a new reading session for the user. Based on the estimated time from the current progress
    /// till the end
    /// </summary>
    public bool GenerateReadingSession { get; init; }
}
