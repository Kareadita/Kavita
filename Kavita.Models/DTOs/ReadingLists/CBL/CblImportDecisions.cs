using System.Collections.Generic;

namespace Kavita.Models.DTOs.ReadingLists.CBL;

/// <summary>
/// Represents a set of decisions against ambiguity in CBL Import
/// </summary>
public sealed record CblImportDecisions
{
    /// <summary>
    /// Per-item user resolutions keyed by the item's Order index
    /// </summary>
    public Dictionary<int, CblItemDecision> ItemResolutions { get; set; } = new();
    /// <summary>
    /// Whether to persist user decisions as remap rules for future imports
    /// </summary>
    public bool SaveAsRemapRules { get; set; } = true;
}

/// <summary>
/// A user's explicit resolution for a single CBL item
/// </summary>
public sealed record CblItemDecision
{
    public int SeriesId { get; set; }
    public int VolumeId { get; set; }
    public int ChapterId { get; set; }
}
