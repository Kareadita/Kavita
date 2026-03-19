using System.Collections.Generic;

namespace Kavita.Models.DTOs.Reader;

/// <summary>
/// This is used for bulk updating a set of volume and or chapters in one go
/// </summary>
public sealed record MarkVolumesReadDto
{
    public int SeriesId { get; set; }
    /// <summary>
    /// A list of Volumes to mark read
    /// </summary>
    public IReadOnlyList<int> VolumeIds { get; set; } = default!;
    /// <summary>
    /// A list of additional Chapters to mark as read
    /// </summary>
    public IReadOnlyList<int> ChapterIds { get; set; } = default!;
    /// <summary>
    /// If true, generates a new reading session for the user. Based on the estimated time from the current progress
    /// till the end
    /// </summary>
    public bool GenerateReadingSession { get; init; }
}
