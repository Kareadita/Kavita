using System;
using System.Collections.Generic;

namespace Kavita.Models.DTOs.Statistics;
#nullable enable

public sealed record ReadingActivityGraphEntryDto
{
    public DateTime Date { get; set; }

    public int TotalTimeReadingSeconds { get; set; }
    public int TotalPages { get; set; }
    public int TotalWords { get; set; }
    public int TotalChaptersFullyRead { get; set; }
}

public class ReadingActivityGraphDto : Dictionary<string, ReadingActivityGraphEntryDto>;
