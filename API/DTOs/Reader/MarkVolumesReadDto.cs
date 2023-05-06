using System.Collections.Generic;

namespace API.DTOs.Reader;

/// <summary>
/// This is used for bulk updating a set of volume and or chapters in one go
/// </summary>
public class MarkVolumesReadDto
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
}
