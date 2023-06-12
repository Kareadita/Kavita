using System;
using API.DTOs.Scrobbling;
using API.Entities.Interfaces;

namespace API.Entities.Scrobble;

/// <summary>
/// Represents an event that would need to be sent to the API layer. These rows will be processed and deleted.
/// </summary>
public class ScrobbleEvent : IEntityDate
{
    public long Id { get; set; }

    public required ScrobbleEventType ScrobbleEventType { get; set; }

    public int? AniListId { get; set; }


    /// <summary>
    /// Rating for the Series
    /// </summary>
    public float? Rating { get; set; }
    public required MediaFormat Format { get; set; }
    /// <summary>
    /// Depends on the ScrobbleEvent if filled in
    /// </summary>
    public int? ChapterNumber { get; set; }
    /// <summary>
    /// Depends on the ScrobbleEvent if filled in
    /// </summary>
    public int? VolumeNumber { get; set; }
    /// <summary>
    /// Has this event been processed and pushed to Provider
    /// </summary>
    public bool IsProcessed { get; set; }
    /// <summary>
    /// The date this was processed
    /// </summary>
    public DateTime? ProcessDateUtc { get; set; }

    public required int SeriesId { get; set; }
    public Series Series { get; set; }

    public required int LibraryId { get; set; }
    public Library Library { get; set; }

    public AppUser AppUser { get; set; }
    public required int AppUserId { get; set; }

    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastModifiedUtc { get; set; }
}
