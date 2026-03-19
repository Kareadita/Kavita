namespace Kavita.Models.DTOs.Reader;

public class MarkChapterReadDto
{
    public int SeriesId { get; init; }
    public int ChapterId { get; init; }
    public bool GenerateReadingSession { get; init; }
}
