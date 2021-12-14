using System;

namespace API.DTOs.Filtering;

/// <summary>
/// Represents the Reading Status. This is a flag and allows multiple statues
/// </summary>
public class ReadStatus
{
    public bool NotRead { get; set; } = false;
    public bool InProgress { get; set; } = false;
    public bool Read { get; set; } = false;
}
