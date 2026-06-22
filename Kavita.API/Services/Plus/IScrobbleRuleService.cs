using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.KavitaPlus.Scrobble;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Scrobble;

namespace Kavita.API.Services.Plus;

/// <summary>
/// Owns the durable read-status transition ledger (<see cref="Models.Entities.History.ScrobbleRuleHistory"/>):
/// hashing rule configuration, guarding the nightly job against re-sending, and recording confirmed deliveries.
/// </summary>
public interface IScrobbleRuleService
{
    /// <summary>
    /// Stable fingerprint of a rule's configuration. Excludes <see cref="ReadStatusTransitionRule.Enabled"/> so
    /// toggling a rule off/on with identical config preserves history. Deterministic across processes.
    /// </summary>
    string ComputeHash(ReadStatusTransitionRule rule);

    /// <summary>
    /// Read-reset: deletes ledger rows for the user where they've read the series/chapter since delivery. Run once
    /// per user at the start of the nightly job so a re-inactive series can fire again.
    /// </summary>
    Task ResetReadSeriesAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Returns the set of (series, chapter) keys already delivered for this user/provider/rule under the current
    /// configuration hash. Keys in this set should be skipped by the nightly job.
    /// </summary>
    Task<HashSet<(int SeriesId, int? ChapterId)>> GetDeliveredKeysAsync(int userId, ScrobbleProvider provider,
        TransitionRuleKind ruleKind, string currentHash, CancellationToken ct = default);

    /// <summary>
    /// Upserts a ledger row for a successfully delivered rule event. No-op for non-rule events. Does not commit -
    /// the caller's unit of work persists it.
    /// </summary>
    Task RecordDeliveredAsync(ScrobbleEvent evt, CancellationToken ct = default);

    /// <summary>
    /// Deletes all ledger rows for a user/provider (provider disconnect).
    /// </summary>
    Task PurgeForProviderAsync(int userId, ScrobbleProvider provider, CancellationToken ct = default);

    /// <summary>
    /// Deletes ledger rows whose hash no longer matches the supplied settings, for both rules. Called when scrobble
    /// settings change so re-configured rules re-evaluate every series.
    /// </summary>
    Task PurgeStaleForSettingsAsync(int userId, ScrobbleProvider provider, ScrobbleProviderSettingsDto settings,
        CancellationToken ct = default);
}
