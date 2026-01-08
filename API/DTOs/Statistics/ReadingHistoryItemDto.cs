using System;
using API.Entities.Enums;

namespace API.DTOs.Statistics;

public sealed record ReadingHistoryItemDto
{
    public int SessionId { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public DateTime LocalDate { get; set; } // For UI grouping by day

    // Series info
    public int SeriesId { get; set; }
    public string SeriesName { get; set; } = string.Empty;
    public MangaFormat SeriesFormat { get; set; }

    // Chapter info
    public int ChapterId { get; set; }
    public string ChapterTitle { get; set; } = string.Empty;
    public string ChapterNumber { get; set; } = string.Empty;

    // Library info
    public int LibraryId { get; set; }
    public string LibraryName { get; set; } = string.Empty;

    // Reading stats for this session
    public int PagesRead { get; set; }
    public int WordsRead { get; set; }
    public int DurationSeconds { get; set; }

    // Progress context
    public int StartPage { get; set; }
    public int EndPage { get; set; }
    public int TotalPages { get; set; }
    public bool Completed { get; set; }
}
