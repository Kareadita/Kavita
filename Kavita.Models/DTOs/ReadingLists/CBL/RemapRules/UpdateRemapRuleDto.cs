namespace Kavita.Models.DTOs.ReadingLists.CBL.RemapRules;
#nullable enable

public sealed record UpdateRemapRuleDto
{
    public string? CblSeriesName { get; set; }
    public int? SeriesId { get; set; }
    public int? VolumeId { get; set; }
    public int? ChapterId { get; set; }
    public string? CblVolume { get; set; }
    public string? CblNumber { get; set; }
}
