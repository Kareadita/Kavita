using System;
using System.Collections.Generic;

namespace API.DTOs.Statistics;

public class UserReadStatistics
{
    /// <summary>
    /// Total number of pages read
    /// </summary>
    public long TotalPagesRead { get; set; }
    /// <summary>
    /// Total time spent reading based on estimates
    /// </summary>
    public long TimeSpentReading { get; set; }
    /// <summary>
    /// A list of genres mapped with genre and number of series that fall into said genre
    /// </summary>
    public ICollection<Tuple<string, long>> FavoriteGenres { get; set; }

    public long ChaptersRead { get; set; }
    public DateTime LastActive { get; set; }
    public long AvgHoursPerWeekSpentReading { get; set; }
}
