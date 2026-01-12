using System;
using System.Collections.Generic;
using API.Entities.Enums;
using API.Services;

namespace API.DTOs.Progress;
#nullable enable

public class DailyReadingDataDto
{
    public int TotalMinutesRead { get; set; }
    public int TotalPagesRead { get; set; }
    public int TotalWordsRead { get; set; }
    public int LongestSessionMinutes { get; set; }

    public List<ReadingActivitySnapshotDto> Activities { get; set; } = [];

    // Data may be deleted
    public IList<int?> SeriesIds { get; set; }
    public IList<int?> ChapterIds { get; set; }
}

public class ReadingActivitySnapshotDto
{
    // Nullable FKs - null means entity was deleted
    public int? SeriesId { get; set; }
    public int? ChapterId { get; set; }
    public int? VolumeId { get; set; }
    public int? LibraryId { get; set; }

    // Denormalized metadata captured at read time
    public string SeriesName { get; set; } = string.Empty;
    /// <summary>
    /// This will be the transformed name from <see cref="EntityNamingService"/>
    /// </summary>
    public string? ChapterTitle { get; set; }
    public string? LibraryName { get; set; }
    public MangaFormat Format { get; set; }

    // Reading metrics for this specific activity
    public int PagesRead { get; set; }
    public int WordsRead { get; set; }
    public int MinutesRead { get; set; }
    public int TotalPages { get; set; }
    public long TotalWords { get; set; }

    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
}
