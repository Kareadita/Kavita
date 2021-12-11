using System;

namespace API.DTOs.Filtering;

/// <summary>
/// Represents the Reading Status. This is a flag and allows multiple statues
/// </summary>
[Flags]
public enum ReadStatus
{
    NotRead = 1,
    InProgress = 2,
    Read = 4,
    All = NotRead | InProgress | Read
}
