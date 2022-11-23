using System;
using System.Collections.Generic;

namespace API.DTOs.Statistics;

public class UserReadStatistics
{
    /// <summary>
    /// Total number of pages read
    /// </summary>
    public long TotalPagesRead { get; set; }
    public ICollection<SeriesDto> AllReadSeries { get; set; }
    public long TimeSpentReading { get; set; }
    public ICollection<Tuple<string, long>> FavoriteGenres { get; set; }
}
