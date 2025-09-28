using System;
using System.Text.Json.Serialization;

namespace API.DTOs.Annotations;

public sealed record FullAnnotationDto
{
    public int Id { get; set; }
    [JsonIgnore]
    public int UserId { get; set; }
    public string SelectedText { get; set; }
    public string? Comment { get; set; }
    public string? CommentHtml { get; set; }
    public string? CommentPlainText { get; set; }
    public string? Context { get; set; }
    public string? ChapterTitle { get; set; }
    public int PageNumber { get; set; }
    public int SelectedSlotIndex { get; set; }
    public bool ContainsSpoiler { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastModifiedUtc { get; set; }

    public int LibraryId { get; set; }
    public string LibraryName { get; set; }
    public int SeriesId { get; set; }
    public string SeriesName { get; set; }
    public int VolumeId { get; set; }
    public string VolumeName { get; set; }
    public int ChapterId { get; set; }
}
