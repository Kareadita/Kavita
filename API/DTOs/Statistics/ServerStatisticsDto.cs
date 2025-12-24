using System.Collections.Generic;

namespace API.DTOs.Statistics;
#nullable enable

public sealed record ServerStatisticsDto
{
    public long ChapterCount { get; set; }
    public long VolumeCount { get; set; }
    public long SeriesCount { get; set; }
    public long TotalFiles { get; set; }
    public long TotalSize { get; set; }
    public long TotalGenres { get; set; }
    public long TotalTags { get; set; }
    public long TotalPeople { get; set; }
    public long TotalReadingTime { get; set; }
}
