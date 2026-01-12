using System;
using System.Collections.Generic;
using API.Entities;
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

    /// <summary>
    /// Detailed breakdown by series/chapter read that day
    /// </summary>
    public List<ReadingActivitySnapshotDto> Activities { get; set; } = [];

    // Data may be deleted, these are legacy identifiers
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
    public string SeriesName { get; set; }
    public string? LocalizedSeriesName { get; set; }
    public string LibraryName { get; set; }
    /// <summary>
    /// Maps to <see cref="Chapter.Range"/>
    /// </summary>
    public string ChapterRange { get; set; }
    /// <summary>
    /// Maps to <see cref="Volume.MinNumber"/>
    /// </summary>
    public float VolumeNumber { get; set; }

    public MangaFormat Format { get; set; }
    public LibraryType LibraryType { get; set; }

    // Reading metrics for this specific activity
    public int PagesRead { get; set; }
    public int WordsRead { get; set; }
    public int MinutesRead { get; set; }

    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
}
