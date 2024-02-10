using System;

namespace API.DTOs.Scrobbling;

public class ScrobbleEventDto
{
    public string SeriesName { get; set; }
    public int SeriesId { get; set; }
    public int LibraryId { get; set; }
    public bool IsProcessed { get; set; }
    public float? VolumeNumber { get; set; }
    public int? ChapterNumber { get; set; }
    public DateTime LastModifiedUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
    public float? Rating { get; set; }
    public ScrobbleEventType ScrobbleEventType { get; set; }
    public bool IsErrored { get; set; }
    public string? ErrorDetails { get; set; }

}
