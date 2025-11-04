using System.Collections.Generic;

namespace API.DTOs.Statistics;
#nullable enable

public sealed record ReadingActivityGraphEntryDto
{
    /// <summary>
    /// Used for the day "cell" inner text
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Used for the day "cell" title attribute, for tooltips and accessibility
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Used for the day "cell" part attribute, as in CSS shadow part for styling purposes
    /// </summary>
    public List<string>? Parts { get; set; }
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
