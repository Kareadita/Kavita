using System;
using API.Entities.Enums;

namespace API.Data.Misc;
#nullable enable

public class RecentlyAddedSeries
{
    public int LibraryId { get; init; }
    public LibraryType LibraryType { get; init; }
    public DateTime Created { get; init; }
    public int SeriesId { get; init; }
    public string? SeriesName { get; init; }
    public MangaFormat Format { get; init; }
    public int ChapterId { get; init; }
    public int VolumeId { get; init; }
    public string? ChapterNumber { get; init; }
    public string? ChapterRange { get; init; }
    public string? ChapterTitle { get; init; }
    public bool IsSpecial { get; init; }
    public float VolumeNumber { get; init; }
    public AgeRating AgeRating { get; init; }
}
