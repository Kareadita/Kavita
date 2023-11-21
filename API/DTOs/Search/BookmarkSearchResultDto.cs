namespace API.DTOs.Search;

public class BookmarkSearchResultDto
{
    public int LibraryId { get; set; }
    public int VolumeId { get; set; }
    public int SeriesId { get; set; }
    public int ChapterId { get; set; }
    public string SeriesName { get; set; }
    public string LocalizedSeriesName { get; set; }
}
