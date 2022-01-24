namespace API.DTOs.Filtering;

/// <summary>
/// Represents the Reading Status. This is a flag and allows multiple statues
/// </summary>
public class ReadStatus
{
    public bool NotRead { get; set; } = true;
    public bool InProgress { get; set; } = true;
    public bool Read { get; set; } = true;
}
