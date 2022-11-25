using System.Collections.Generic;

namespace API.DTOs.Statistics;

public class ServerStatistics
{
    public long ChapterCount { get; set; }
    public long VolumeCount { get; set; }
    public long SeriesCount { get; set; }
    public long TotalFiles { get; set; }
    public int TotalReadingLists { get; set; }
    public int TotalWantToReadSeries { get; set; }
}
