using API.Entities.Enums;

namespace API.DTOs;

/// <summary>
/// Used on Person Profile page
/// </summary>
public class StandaloneChapterDto : ChapterDto
{
    public int SeriesId { get; set; }
    public int LibraryId { get; set; }
    public LibraryType LibraryType { get; set; }
    public string VolumeTitle { get; set; }
}
