using System;
using System.Collections.Generic;
using Kavita.Models.DTOs.KavitaPlus.ExternalMetadata;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.KavitaPlus;
using Kavita.Models.Entities.Enums.UserPreferences;
using Kavita.Models.Entities.Scrobble;

namespace Kavita.Models.DTOs.KavitaPlus.Scrobble;
#nullable enable

public record ScrobbleV3Dto: MetadataRequest
{
    public required ScrobbleProvider Provider { get; set; }
    public required string AuthenticationToken { get; set; }

    /// <summary>
    /// Name of the series
    /// </summary>
    /// <remarks>This may be a book's name if <see cref="MetadataRequest.IsStandAlone"/> is true</remarks>
    public required string SeriesName { get; set; }
    public List<string> AlternativeNames { get; set; } = [];
    public required PlusMediaFormat Format { get; set; }
    public int? Year { get; set; }


    /// <summary>
    /// The language of the entity being read. I.e. series for AniList, Book(Chapter) for Hardcover
    /// </summary>
    /// <remarks>Defaults to English</remarks>
    public string? Language { get; set; }

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
    /// Number of pages read
    /// </summary>
    /// <remarks>This is relevant when scrobbling to Hardcover, this will be converted to pages based on the edition page count</remarks>
    public int? PercentRead { get; set; }
    /// <summary>
    /// Total number of times the Series has been read
    /// </summary>
    public int? TotalReadCountForSeries { get; set; }
    /// <summary>
    /// Rating for the Series
    /// </summary>
    /// <remarks>This will map based on user's preferences</remarks>
    public float? Rating { get; set; }
    /// <summary>
    /// The date that the series was started reading. Will be null for non ReadingProgress events
    /// </summary>
    public DateTime? StartedReadingDateUtc { get; set; }
    /// <summary>
    /// The latest date the series was read. Will be null for non ReadingProgress events
    /// </summary>
    /// <remarks>Introduced in Kavita v0.7.6</remarks>
    public DateTime? LatestReadingDateUtc { get; set; }
    /// <summary>
    /// The date that the series was scrobbled. Will be null for non ReadingProgress events
    /// </summary>
    public DateTime? ScrobbleDateUtc { get; set; }
    public string? ReviewTitle { get; set; }
    public string? ReviewBody { get; set; }
    public ReviewScrobbleTarget? ReviewScrobbleTarget { get; set; }
    public ScrobbleReadStatus? ReadStatus { get; set; }
}
