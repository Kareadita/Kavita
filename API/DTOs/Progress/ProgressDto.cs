using System;

namespace API.DTOs.Progress;
#nullable enable

public sealed record ProgressDto
{
    public required int VolumeId { get; set; }
    public required int ChapterId { get; set; }
    public required int PageNum { get; set; }
    public required int SeriesId { get; set; }
    public required int LibraryId { get; set; }
    /// <summary>
    /// For EPUB reader, this can be an optional string of the id of a part marker, to help resume reading position
    /// on pages that combine multiple "chapters".
    /// </summary>
    public string? BookScrollId { get; set; }

    public DateTime LastModifiedUtc { get; set; }
}
