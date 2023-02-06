using System;
using System.ComponentModel.DataAnnotations;

namespace API.DTOs;

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
    /// For Book reader, this can be an optional string of the id of a part marker, to help resume reading position
    /// on pages that combine multiple "chapters".
    /// </summary>
    public string BookScrollId { get; set; }
    /// <summary>
    /// Last time Chapter was read in Utc.
    /// </summary>
    public DateTime LastModified { get; set; }
    /// <summary>
    /// Last time Chapter was read.
    /// </summary>
    public DateTime LastModifiedUtc { get; set; }
}
