namespace API.DTOs.Reader;

/// <summary>
/// A range of time to read a selection (series, chapter, etc)
/// </summary>
public record HourEstimateRangeDto
{
    /// <summary>
    /// Min hours to read the selection
    /// </summary>
    public int MinHours { get; init; } = 1;
    /// <summary>
    /// Max hours to read the selection
    /// </summary>
    public int MaxHours { get; init; } = 1;
    /// <summary>
    /// Estimated average hours to read the selection
    /// </summary>
    public float AvgHours { get; init; } = 1f;
}
