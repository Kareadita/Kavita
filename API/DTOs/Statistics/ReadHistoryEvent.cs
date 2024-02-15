using System;

namespace API.DTOs.Statistics;

/// <summary>
/// Represents a single User's reading event
/// </summary>
public class ReadHistoryEvent
{
    public int UserId { get; set; }
    public required string? UserName { get; set; } = default!;
    public int LibraryId { get; set; }
    public int SeriesId { get; set; }
    public required string SeriesName { get; set; } = default!;
    public DateTime ReadDate { get; set; }
    public int ChapterId { get; set; }
    public required float ChapterNumber { get; set; } = default!;
}
