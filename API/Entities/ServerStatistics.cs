namespace API.Entities;

public class ServerStatistics
{
    public int Id { get; set; }
    public int Year { get; set; }
    public long SeriesCount { get; set; }
    public long VolumeCount { get; set; }
    public long ChapterCount { get; set; }
    public long FileCount { get; set; }
    public long UserCount { get; set; }
    public long GenreCount { get; set; }
    public long PersonCount { get; set; }
    public long TagCount { get; set; }
}
