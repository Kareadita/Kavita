namespace Kavita.Models.DTOs.ReadingLists.CBL.RemapRules;
#nullable enable

public sealed record UpdateRemapRuleDto
{
    public int? VolumeId { get; set; }
    public int? ChapterId { get; set; }
    public string? CblVolume { get; set; }
    public string? CblNumber { get; set; }
}
