using API.Entities.Enums;

namespace API.DTOs.Reader;

public class BookInfoDto : IChapterInfoDto
{
    public string BookTitle { get; set; }
    public int SeriesId { get; set; }
    public int VolumeId { get; set; }
    public MangaFormat SeriesFormat { get; set; }
    public string SeriesName { get; set; }
    public string ChapterNumber { get; set; }
    public string VolumeNumber { get; set; }
    public int LibraryId { get; set; }
    public int Pages { get; set; }
    public bool IsSpecial { get; set; }
    public string ChapterTitle { get; set; }
}
