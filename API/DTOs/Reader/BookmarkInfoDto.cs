using API.Entities.Enums;

namespace API.DTOs.Reader;

public class BookmarkInfoDto
{
    public string SeriesName { get; set; } = default!;
    public MangaFormat SeriesFormat { get; set; }
    public int SeriesId { get; set; }
    public int LibraryId { get; set; }
    public LibraryType LibraryType { get; set; }
    public int Pages { get; set; }
}
