namespace Kavita.Models.DTOs.ReadingLists.CBL.RemapRules;
#nullable enable

public sealed record CreateRemapRuleDto
{
    /// <summary>
    /// The CBL series name as it appears in the file, will be normalized server-side
    /// </summary>
    public string CblSeriesName { get; set; } = string.Empty;
    public int SeriesId { get; set; }
    /// <summary>
    /// Optional: CBL volume string for issue/volume-level rules
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
