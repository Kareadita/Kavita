using System;
using System.Collections.Generic;

namespace API.DTOs.Statistics;
#nullable enable

public sealed record UserReadStatistics
{
    /// <summary>
    /// Total number of pages read
    /// </summary>
    public long TotalPagesRead { get; set; }
    /// <summary>
    /// Total number of words read
    /// </summary>
    public long TotalWordsRead { get; set; }
    /// <summary>
    /// Total time spent reading
    /// </summary>
    public long TimeSpentReading { get; set; }
    /// <summary>
    /// Last time user read anything
    /// </summary>
    public DateTime? LastActiveUtc { get; set; }
    public double AvgHoursPerWeekSpentReading { get; set; }

}
