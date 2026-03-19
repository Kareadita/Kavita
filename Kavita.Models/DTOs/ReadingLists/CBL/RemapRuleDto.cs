using System;

namespace Kavita.Models.DTOs.ReadingLists.CBL;

public record RemapRuleDto
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

public record CreateRemapRuleDto
{
    /// <summary>
    /// The CBL series name as it appears in the file — will be normalized server-side
    /// </summary>
    public string CblSeriesName { get; set; } = string.Empty;
    public int SeriesId { get; set; }
    /// <summary>
    /// Optional: CBL volume string for issue-level rules
    /// </summary>
    public string? CblVolume { get; set; }
    /// <summary>
    /// Optional: CBL issue number string for issue-level rules
    /// </summary>
    public string? CblNumber { get; set; }
    /// <summary>
    /// Optional: Kavita Volume ID for issue-level rules
    /// </summary>
    public int? VolumeId { get; set; }
    /// <summary>
    /// Optional: Kavita Chapter ID for issue-level rules
    /// </summary>
    public int? ChapterId { get; set; }
}

public record UpdateRemapRuleDto
{
    public int? VolumeId { get; set; }
    public int? ChapterId { get; set; }
    public string? CblVolume { get; set; }
    public string? CblNumber { get; set; }
}
