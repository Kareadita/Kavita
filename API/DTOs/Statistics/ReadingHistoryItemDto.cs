using System;
using System.Collections.Generic;
using API.Entities.Enums;

namespace API.DTOs.Statistics;

public sealed record ReadingHistoryItemDto
{
    public List<int> SessionDataIds { get; set; }
    public int SessionId { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public DateTime LocalDate { get; set; } // For UI grouping by day

    // Series info
    public int SeriesId { get; set; }
    public string SeriesName { get; set; } = string.Empty;
    public MangaFormat SeriesFormat { get; set; }

    // Chapter info
    public List<ReadingHistoryChapterItemDto> Chapters { get; set; }

    // Library info
    public int LibraryId { get; set; }
    public string LibraryName { get; set; } = string.Empty;

    // Reading stats for this session
    public int PagesRead { get; set; }
    public int WordsRead { get; set; }
    public int DurationSeconds { get; set; }

    public int TotalPages { get; set; }
}

public sealed record ReadingHistoryChapterItemDto
{
    public int ChapterId { get; set; }
    public string Label { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }

    public int PagesRead { get; set; }
    public int WordsRead { get; set; }
    public int DurationSeconds { get; set; }

    public int StartPage { get; set; }
    public int EndPage { get; set; }
    public int TotalPages { get; set; }
    public bool Completed { get; set; }
}
