#nullable enable
using System;
using Kavita.Models.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs.SeriesDetail;

public class RecentlyAddedSeriesDto
{
    public int LibraryId { get; init; }
    [EnumDataType(typeof(LibraryType))]
    public LibraryType LibraryType { get; init; }
    public DateTime Created { get; init; }
    public int SeriesId { get; init; }
    public string? SeriesName { get; init; }
    public string LocalizedSeriesName { get; set; } = string.Empty;
    [EnumDataType(typeof(MangaFormat))]
    public MangaFormat Format { get; init; }
    public int ChapterId { get; init; }
    public int VolumeId { get; init; }
    public string? ChapterNumber { get; init; }
    public string? ChapterRange { get; init; }
    public string? ChapterTitle { get; init; }
    public bool IsSpecial { get; init; }
    public float VolumeNumber { get; init; }
    [EnumDataType(typeof(AgeRating))]
    public AgeRating AgeRating { get; init; }
}
