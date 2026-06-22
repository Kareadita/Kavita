using System;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.KavitaPlus;
using Kavita.Models.Entities.Enums.UserPreferences;
using Kavita.Models.Entities.Interfaces;
using Kavita.Models.Entities.User;

namespace Kavita.Models.Entities.Scrobble;
#nullable enable

/// <summary>
/// Represents an event that would need to be sent to the API layer. These rows will be processed and deleted.
/// </summary>
public class ScrobbleEvent : IEntityDate
{
    public long Id { get; set; }

    public required ScrobbleEventType ScrobbleEventType { get; set; }
    /// <summary>
    /// The provider for this event
    /// </summary>
    public ScrobbleProvider ScrobbleProvider { get; set; }

    public int? AniListId { get; set; }
    public long? MalId { get; set; }
    public long? MangabakaId { get; set; }
    /// <remarks>This **MUST** be the book id, not series id!</remarks>
    public int? HardcoverId { get; set; }

    /// <summary>
    /// Rating for the Series
    /// </summary>
    public float? Rating { get; set; }
    /// <summary>
    /// Review for the Series
    /// </summary>
    public string? ReviewBody { get; set; }
    public string? ReviewTitle { get; set; }
    public required PlusMediaFormat Format { get; set; }
    /// <summary>
    /// Depends on the ScrobbleEvent if filled in
    /// </summary>
    public int? ChapterNumber { get; set; }
    /// <summary>
    /// Depends on the ScrobbleEvent if filled in
    /// </summary>
    public float? VolumeNumber { get; set; }
    /// <summary>
    /// The % on the chapter (This is for Chapter-based tracking, i.e. Hardcover)
    /// </summary>
    public float? Progress { get; set; }
    /// <summary>
    /// The status to set the entity to
    /// </summary>
    public ScrobbleReadStatus? ReadStatus { get; set; }
    /// <summary>
    /// True if the event was created due to a backfill
    /// </summary>
    /// <remarks>When overriding by a non backfill event should be set to false</remarks>
    public bool IsBackFill { get; set; }
    /// <summary>
    /// Which read-status transition rule produced this event. Null when the event did not originate from
    /// <c>RunReadStatusTransitionRules</c>. Carried across the create -> deliver gap so the delivery step can
    /// write a <see cref="History.ScrobbleRuleHistory"/> row.
    /// </summary>
    public TransitionRuleKind? TransitionRuleKind { get; set; }
    /// <summary>
    /// Snapshot of the rule's configuration hash at fire-time, pinned so the ledger row records the exact
    /// configuration that triggered it (rather than whatever the config is at delivery time).
    /// </summary>
    public string? RuleHashSnapshot { get; set; }
    /// <summary>
    /// Has this event been processed and pushed to Provider
    /// </summary>
    public bool IsProcessed { get; set; }
    /// <summary>
    /// Was there an error processing this event
    /// </summary>
    public bool IsErrored { get; set; }
    /// <summary>
    /// The error details
    /// </summary>
    public string? ErrorDetails { get; set; }
    /// <summary>
    /// The date this was processed
    /// </summary>
    public DateTime? ProcessDateUtc { get; set; }


    public required int SeriesId { get; set; }
    public Series Series { get; set; }

    public int? ChapterId { get; set; }
    public Chapter? Chapter { get; set; }

    public required int LibraryId { get; set; }
    public Library Library { get; set; }

    public AppUser AppUser { get; set; }
    public required int AppUserId { get; set; }

    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastModifiedUtc { get; set; }

    /// <summary>
    /// Sets the ErrorDetail and marks the event as <see cref="IsErrored"/>
    /// </summary>
    /// <param name="errorMessage"></param>
    public void SetErrorMessage(string errorMessage)
    {
        ErrorDetails = errorMessage;
        IsErrored = true;
    }
}
