using System;
using Kavita.Models.Entities.Enums.ReadingList;
using Kavita.Models.Entities.User;

namespace Kavita.Models.Entities.ReadingLists;
#nullable enable

/// <summary>
/// Persists a user's (or admin's) decision mapping a CBL series/issue name to a Kavita entity.
/// Used as Tier 0 in the CBL matching pipeline.
/// </summary>
public class ReadingListRemapRule
{
    public int Id { get; set; }

    /// <summary>
    /// The normalized CBL series name that this rule matches against
    /// </summary>
    public required string NormalizedCblSeriesName { get; set; }

    /// <summary>
    /// Optional CBL volume to narrow matching (null = any volume)
    /// </summary>
    public string? CblVolume { get; set; }

    /// <summary>
    /// Optional CBL issue number to narrow matching (null = any issue)
    /// </summary>
    public string? CblNumber { get; set; }

    /// <summary>
    /// The Kavita Series this rule maps to
    /// </summary>
    public int SeriesId { get; set; }
    public Series Series { get; set; } = null!;

    /// <summary>
    /// Optional: specific Volume within the Series
    /// </summary>
    public int? VolumeId { get; set; }
    public Volume? Volume { get; set; }

    /// <summary>
    /// Optional: specific Chapter within the Volume
    /// </summary>
    public int? ChapterId { get; set; }
    public Chapter? Chapter { get; set; }

    /// <summary>
    /// The original CBL series name as it appeared in the file (for display)
    /// </summary>
    public string CblSeriesName { get; set; } = string.Empty;

    /// <summary>
    /// Snapshot of the series name at time of mapping creation (for auditing)
    /// </summary>
    public string SeriesNameAtMapping { get; set; } = string.Empty;

    /// <summary>
    /// When true, this rule is visible to all users (admin-promoted).
    /// AppUserId still tracks the original creator.
    /// </summary>
    public bool IsGlobal { get; set; }

    /// <summary>
    /// The user who created this rule. Always required.
    /// </summary>
    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;

    public DateTime CreatedUtc { get; set; }

    public CblRemapRuleKind GetKind() =>
        ChapterId != null ? CblRemapRuleKind.Chapter :
        VolumeId != null ? CblRemapRuleKind.Volume :
        CblRemapRuleKind.Series;
}
