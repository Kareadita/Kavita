using System;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.ReadingList;

namespace Kavita.Models.DTOs.ReadingLists.CBL.RemapRules;
#nullable enable

public sealed record RemapRuleDto
{
    public int Id { get; set; }
    public string NormalizedCblSeriesName { get; set; } = string.Empty;
    public string CblSeriesName { get; set; } = string.Empty;
    public string? CblVolume { get; set; }
    public string? CblNumber { get; set; }
    public int SeriesId { get; set; }
    public int? VolumeId { get; set; }
    public string VolumeNumber { get; set; } = string.Empty;
    public int? ChapterId { get; set; }
    public CblRemapRuleKind Kind { get; set; }
    public string ChapterRange { get; set; } = string.Empty;
    public string ChapterTitleName { get; set; } = string.Empty;
    public bool ChapterIsSpecial { get; set; }
    public LibraryType LibraryType { get; set; }
    public string SeriesNameAtMapping { get; set; } = string.Empty;
    public int AppUserId { get; set; }
    public bool IsGlobal { get; set; }
    public string CreatedByUserName { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}
