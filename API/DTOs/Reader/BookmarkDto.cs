using System.ComponentModel.DataAnnotations;

namespace API.DTOs.Reader;
#nullable enable

public class BookmarkDto
{
    public int Id { get; set; }
    [Required]
    public int Page { get; set; }
    [Required]
    public int VolumeId { get; set; }
    [Required]
    public int SeriesId { get; set; }
    [Required]
    public int ChapterId { get; set; }
    /// <summary>
    /// This is only used when getting all bookmarks.
    /// </summary>
    public SeriesDto? Series { get; set; }
}
