namespace API.DTOs.Statistics;

public class KavitaPlusMetadataBreakdownDto
{
    /// <summary>
    /// Total amount of Series
    /// </summary>
    public int TotalSeries { get; set; }
    /// <summary>
    /// Series on the Blacklist (errored or bad match)
    /// </summary>
    public int ErroredSeries { get; set; }
    /// <summary>
    /// Completed so far
    /// </summary>
    public int SeriesCompleted { get; set; }
}
