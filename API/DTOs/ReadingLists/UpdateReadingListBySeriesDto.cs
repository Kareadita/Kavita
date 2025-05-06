namespace API.DTOs.ReadingLists;

public sealed record UpdateReadingListBySeriesDto
{
    public int SeriesId { get; init; }
    public int ReadingListId { get; init; }
}
