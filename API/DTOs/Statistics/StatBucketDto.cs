namespace API.DTOs.Statistics;

/// <summary>
/// A bucket of items (fixed) from 0-X, X-X*2
/// </summary>
public sealed record StatBucketDto
{
    public int RangeStart { get; set; }
    /// <summary>
    /// Null for the last range (1000+)
    /// </summary>
    public int? RangeEnd { get; set; }
    public int Count { get; set; }
    /// <summary>
    /// Percentage of total chapters
    /// </summary>
    public decimal Percentage { get; set; }
}


