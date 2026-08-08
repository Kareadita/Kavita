using Kavita.Models.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs;
#nullable enable

/// <summary>
/// Used on Person Profile page
/// </summary>
public class StandaloneChapterDto : ChapterDto
{
    public int SeriesId { get; set; }
    public int LibraryId { get; set; }
    [EnumDataType(typeof(LibraryType))]
    public LibraryType LibraryType { get; set; }
    public string VolumeTitle { get; set; }
}