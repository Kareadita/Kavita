using System.Collections.Generic;

namespace API.DTOs.SeriesDetail;
#nullable enable

/// <summary>
/// This is a special DTO for a UI page in Kavita. This performs sorting and grouping and returns exactly what UI requires for layout.
/// This is subject to change, do not rely on this Data model.
/// </summary>
public class SeriesDetailDto
{
    /// <summary>
    /// Specials for the Series. These will have their title and range cleaned to remove the special marker and prepare
    /// </summary>
    public IEnumerable<ChapterDto> Specials { get; set; } = default!;
    /// <summary>
    /// All Chapters, excluding Specials and single chapters (0 chapter) for a volume
    /// </summary>
    public IEnumerable<ChapterDto> Chapters { get; set; } = default!;
    /// <summary>
    /// Just the Volumes for the Series (Excludes Volume 0)
    /// </summary>
    public IEnumerable<VolumeDto> Volumes { get; set; } = default!;
    /// <summary>
    /// These are chapters that are in Volume 0 and should be read AFTER the volumes
    /// </summary>
    public IEnumerable<ChapterDto> StorylineChapters { get; set; } = default!;
    /// <summary>
    /// How many chapters are unread
    /// </summary>
    public int UnreadCount { get; set; }
    /// <summary>
    /// How many chapters are there
    /// </summary>
    public int TotalCount { get; set; }
}
