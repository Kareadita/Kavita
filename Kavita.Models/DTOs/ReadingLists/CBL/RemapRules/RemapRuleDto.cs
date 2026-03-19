using System;

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
    public int? ChapterId { get; set; }
    public string SeriesNameAtMapping { get; set; } = string.Empty;
    public int? AppUserId { get; set; }
    public DateTime CreatedUtc { get; set; }
}
