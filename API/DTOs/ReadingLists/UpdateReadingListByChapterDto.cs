namespace API.DTOs.ReadingLists;

public class UpdateReadingListByChapterDto
{
    public int ChapterId { get; init; }
    public int SeriesId { get; init; }
    public int ReadingListId { get; init; }
}
