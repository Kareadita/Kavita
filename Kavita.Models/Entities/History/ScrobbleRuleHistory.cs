using System;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Scrobble;
using Kavita.Models.Entities.User;

namespace Kavita.Models.Entities.History;
#nullable enable

/// <summary>
/// Durable ledger of read-status transitions that have been confirmed delivered to a provider.
/// Guards <c>RunReadStatusTransitionRules</c> from re-sending the same On Hold / Dropped transition
/// on every nightly run. A row is written only once the underlying <see cref="ScrobbleEvent"/> has
/// been successfully POSTed.
/// </summary>
/// <remarks>
/// Unlike <see cref="ScrobbleEvent"/> (a transient outbox that is reaped 7 days after processing), this
/// table is permanent. The <see cref="RuleHash"/> is a staleness fingerprint of the rule's configuration,
/// not the lookup key - the key is the relational columns.
/// </remarks>
public class ScrobbleRuleHistory
{
    public long Id { get; set; }

    /// <summary>
    /// The user the rule fired for. Eligibility is computed per-user.
    /// </summary>
    public required int AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;

    /// <summary>
    /// The provider the transition was delivered to
    /// </summary>
    public required ScrobbleProvider Provider { get; set; }

    /// <summary>
    /// Which transition rule produced this row
    /// </summary>
    public required TransitionRuleKind RuleKind { get; set; }

    public required int SeriesId { get; set; }
    public Series Series { get; set; } = null!;

    /// <summary>
    /// Set for chapter-based providers (Hardcover); null for series-based providers
    /// </summary>
    public int? ChapterId { get; set; }
    public Chapter? Chapter { get; set; }

    /// <summary>
    /// Staleness fingerprint of the rule configuration that fired (excludes Enabled). A mismatch against the
    /// current rule hash means the configuration changed and the series should be re-evaluated.
    /// </summary>
    public required string RuleHash { get; set; }

    /// <summary>
    /// When the transition was confirmed delivered. Acts as the staleness anchor: if the user's last progress
    /// is newer than this, they've read since delivery and the rule may fire again.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// The event that produced this row, for traceability. Nullable and ON DELETE SET NULL because the event
    /// is reaped ~7 days after processing while this row must outlive it.
    /// </summary>
    public long? ScrobbleEventId { get; set; }
    public ScrobbleEvent? ScrobbleEvent { get; set; }
}
