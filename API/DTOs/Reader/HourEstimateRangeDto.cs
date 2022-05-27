namespace API.DTOs.Reader;

/// <summary>
/// A range of time to read a selection (series, chapter, etc)
/// </summary>
public class HourEstimateRangeDto
{
    /// <summary>
    /// Min hours to read the selection
    /// </summary>
    public int MinHours { get; set; } = 1;
    /// <summary>
    /// Max hours to read the selection
    /// </summary>
    public int MaxHours { get; set; } = 1;
    /// <summary>
    /// Estimated average hours to read the selection
    /// </summary>
    public int AvgHours { get; set; } = 1;
    /// <summary>
    /// Does the user have progress on the range this represents
    /// </summary>
    public bool HasProgress { get; set; } = false;
}
