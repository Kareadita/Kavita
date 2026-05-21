using System;
using Kavita.Models.Entities.Enums.Audit;
using Kavita.Models.Entities.User;

namespace Kavita.Models.Entities.History;
#nullable enable

/// <summary>
/// Records a durable, queryable log of every significant Kavita+ event:
/// matching, metadata writes, scrobble sends, collection syncs, and people updates.
/// </summary>
public class KavitaPlusAuditLog
{
    public long Id { get; set; }
    public DateTime CreatedUtc { get; set; }

    public KavitaPlusAuditCategory Category { get; set; }
    public KavitaPlusEventType EventType { get; set; }
    public AuditStatus Status { get; set; }

    /// <summary>
    /// Series FK - set for Series, Chapter, and series-contextual events. No cascade delete: logs outlive entities
    /// </summary>
    public int? SeriesId { get; set; }

    /// <summary>
    /// Discriminator describing what SubjectId refers to
    /// </summary>
    public AuditSubjectType SubjectType { get; set; }

    /// <summary>PersonId, CollectionId, or ChapterId depending on SubjectType. Null for Series/Global events</summary>
    public int? SubjectId { get; set; }

    /// <summary>
    /// JSON-serialized event-specific payload.
    /// </summary>
    public string? Payload { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Scrobble events that failed allow retrying
    /// </summary>
    public bool HasRetried { get; set; }

    /// <summary>
    /// The user who triggered this event. Null for system-initiated events.
    /// No cascade delete: logs outlive users.
    /// </summary>
    public int? UserId { get; set; }
    public AppUser? User { get; set; }
}
