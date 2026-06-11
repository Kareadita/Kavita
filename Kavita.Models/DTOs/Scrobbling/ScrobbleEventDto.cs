using System;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.KavitaPlus;
using Kavita.Models.Entities.Enums.UserPreferences;

namespace Kavita.Models.DTOs.Scrobbling;
#nullable enable

public sealed record ScrobbleEventDto
{
    public long Id { get; init; }
    public string SeriesName { get; set; }
    public int SeriesId { get; set; }
    public int LibraryId { get; set; }
    public bool IsProcessed { get; set; }
    public float? VolumeNumber { get; set; }
    public int? ChapterNumber { get; set; }
    public DateTime LastModifiedUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
    public float? Rating { get; set; }
    public ScrobbleReadStatus? ReadStatus { get; set; }
    public ScrobbleEventType ScrobbleEventType { get; set; }
    public ScrobbleProvider ScrobbleProvider { get; set; }
    public bool IsErrored { get; set; }
    public string? ErrorDetails { get; set; }

}
