namespace API.DTOs.Reader;

/// <summary>
/// A range of time
/// </summary>
public class HourEstimateRangeDto
{
    public int MinHours { get; set; } = 1;
    public int MaxHours { get; set; } = 1;
    public int AvgHours { get; set; } = 1;
}
