using System.Collections.Generic;

namespace API.DTOs.Statistics;
#nullable enable

public class ServerStatisticsDto
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
    public IEnumerable<ICount<SeriesDto>>? MostReadSeries { get; set; }
    /// <summary>
    /// Total users who have started/reading/read per series
    /// </summary>
    public IEnumerable<ICount<SeriesDto>>? MostPopularSeries { get; set; }
    public IEnumerable<ICount<UserDto>>? MostActiveUsers { get; set; }
    public IEnumerable<ICount<LibraryDto>>? MostActiveLibraries { get; set; }
    /// <summary>
    /// Last 5 Series read
    /// </summary>
    public IEnumerable<SeriesDto>? RecentlyRead { get; set; }


}
