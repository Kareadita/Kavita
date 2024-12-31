using System;
using System.ComponentModel;
using API.DTOs.Recommendation;

namespace API.DTOs.Scrobbling;
#nullable enable

public enum ScrobbleEventType
{
    [Description("Chapter Read")]
    ChapterRead = 0,
    [Description("Add to Want to Read")]
    AddWantToRead = 1,
    [Description("Remove from Want to Read")]
    RemoveWantToRead = 2,
    [Description("Score Updated")]
    ScoreUpdated = 3,
    [Description("Review Added/Updated")]
    Review = 4
}

/// <summary>
/// Represents PlusMediaFormat
/// </summary>
public enum PlusMediaFormat
{
    [Description("Manga")]
    Manga = 1,
    [Description("Comic")]
    Comic = 2,
    [Description("LightNovel")]
    LightNovel = 3,
    [Description("Book")]
    Book = 4,
    Unknown = 5
}


public class ScrobbleDto
{
    /// <summary>
    /// User's access token to allow us to talk on their behalf
    /// </summary>
    public string AniListToken { get; set; }
    public string SeriesName { get; set; }
    public string LocalizedSeriesName { get; set; }
    public PlusMediaFormat Format { get; set; }
    public int? Year { get; set; }
    /// <summary>
    /// Optional AniListId if present on Kavita's WebLinks
    /// </summary>
    public int? AniListId { get; set; } = 0;
    public int? MALId { get; set; } = 0;
    public string BakaUpdatesId { get; set; } = string.Empty;

    public ScrobbleEventType ScrobbleEventType { get; set; }
    /// <summary>
    /// Number of chapters read
    /// </summary>
    /// <remarks>If completed series, this can consider the Series Read (AniList)</remarks>
    public int? ChapterNumber { get; set; }
    /// <summary>
    /// Number of Volumes read
    /// </summary>
    /// <remarks>This will not consider the series Completed, even if all Volumes have been read (AniList)</remarks>
    public int? VolumeNumber { get; set; }
    /// <summary>
    /// Rating for the Series
    /// </summary>
    public float? Rating { get; set; }
    public string? ReviewTitle { get; set; }
    public string? ReviewBody { get; set; }
    /// <summary>
    /// The date that the series was started reading. Will be null for non ReadingProgress events
    /// </summary>
    public DateTime? StartedReadingDateUtc { get; set; }
    /// <summary>
    /// The latest date the series was read. Will be null for non ReadingProgress events
    /// </summary>
    public DateTime? LatestReadingDateUtc { get; set; }
    /// <summary>
    /// The date that the series was scrobbled. Will be null for non ReadingProgress events
    /// </summary>
    public DateTime? ScrobbleDateUtc { get; set; }
    /// <summary>
    /// Optional but can help with matching
    /// </summary>
    public string? Isbn { get; set; }

}
