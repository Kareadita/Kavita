using System;

namespace API.DTOs.Scrobbling;

/// <summary>
///
/// </summary>
public class ScrobbleEventDto
{
    public int SeriesId { get; set; }
    public bool IsProcessed { get; set; }
    public int? VolumeNumber { get; set; }
    public int? ChapterNumber { get; set; }
    public DateTime? ProcessDateUtc { get; set; }
    public float? Rating { get; set; }
    public ScrobbleEventType ScrobbleEventType { get; set; }
}
