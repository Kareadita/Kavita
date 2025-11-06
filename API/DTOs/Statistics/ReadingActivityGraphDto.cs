using System;
using System.Collections.Generic;

namespace API.DTOs.Statistics;
#nullable enable

public sealed record ReadingActivityGraphEntryDto
{
    public DateTime Date { get; set; }

    /// <summary>
    /// Extra data that needs to be packed in
    /// </summary>
    public ReadingActivityGraphExtraDataDto ExtraData { get; set; }
}

public sealed record ReadingActivityGraphExtraDataDto
{
    public int TotalTimeReadingSeconds { get; set; }
    public int TotalPages { get; set; }
    public int TotalWords { get; set; }
    public int TotalChaptersFullyRead { get; set; }
}

public class ReadingActivityGraphDto : Dictionary<string, ReadingActivityGraphEntryDto>;
