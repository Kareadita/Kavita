using System.Collections.Generic;

namespace API.DTOs;

/// <summary>
/// This is a special DTO for a UI page in Kavita. This performs sorting and grouping and returns exactly what UI requires for layout.
/// This is subject to change, do not rely on this API.
/// </summary>
public class SeriesDetailDto
{
    /// <summary>
    /// Specials for the Series. These will have their title and range cleaned to remove the special marker and prepare
    /// </summary>
    public IEnumerable<ChapterDto> Specials { get; set; }
    public IEnumerable<ChapterDto> Chapters { get; set; }
    public IEnumerable<VolumeDto> Volumes { get; set; }
    /// <summary>
    /// These are chapters that are in Volume 0 and should be read AFTER the volumes
    /// </summary>
    public IEnumerable<ChapterDto> StorylineChapters { get; set; }

}
