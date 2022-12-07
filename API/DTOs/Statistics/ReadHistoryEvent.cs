using System;

namespace API.DTOs.Statistics;

/// <summary>
/// Represents a single User's reading event
/// </summary>
public class ReadHistoryEvent
{
    public int UserId { get; set; }
    public string UserName { get; set; }
    public int LibraryId { get; set; }
    public int SeriesId { get; set; }
    public string SeriesName { get; set; }
    public DateTime ReadDate { get; set; }
    public int ChapterId { get; set; }
    public string ChapterNumber { get; set; }
}
