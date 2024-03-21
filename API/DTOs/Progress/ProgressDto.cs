using System;
using System.ComponentModel.DataAnnotations;

namespace API.DTOs.Progress;
#nullable enable

public class ProgressDto
{
    [Required]
    public int VolumeId { get; set; }
    [Required]
    public int ChapterId { get; set; }
    [Required]
    public int PageNum { get; set; }
    [Required]
    public int SeriesId { get; set; }
    [Required]
    public int LibraryId { get; set; }
    /// <summary>
    /// For EPUB reader, this can be an optional string of the id of a part marker, to help resume reading position
    /// on pages that combine multiple "chapters".
    /// </summary>
    public string? BookScrollId { get; set; }

    public DateTime LastModifiedUtc { get; set; }
}
