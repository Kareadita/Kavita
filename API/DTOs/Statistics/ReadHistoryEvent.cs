using System;

namespace API.DTOs.Statistics;
#nullable enable

/// <summary>
/// Represents a single User's reading event
/// </summary>
public sealed record ReadHistoryEvent
{
    public int UserId { get; set; }
    public required string? UserName { get; set; } = default!;
    public int LibraryId { get; set; }
    public int SeriesId { get; set; }
    public required string SeriesName { get; set; } = default!;
    public DateTime ReadDate { get; set; }
    public DateTime ReadDateUtc { get; set; }
    public int ChapterId { get; set; }
    public required float ChapterNumber { get; set; } = default!;
}
