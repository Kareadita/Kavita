using System.ComponentModel.DataAnnotations;

namespace API.DTOs.Reader;
#nullable enable

public sealed record BookmarkDto
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
    /// Only applicable for Epubs
    /// </summary>
    public int ImageOffset { get; set; }
    /// <summary>
    /// Only applicable for Epubs
    /// </summary>
    public string? XPath { get; set; }
    /// <summary>
    /// This is only used when getting all bookmarks.
    /// </summary>
    public SeriesDto? Series { get; set; }
    /// <summary>
    /// Not required, will be filled out at API before saving to the DB
    /// </summary>
    public string? ChapterTitle { get; set; }
}
