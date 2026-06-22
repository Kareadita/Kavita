using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.KavitaPlus;
using Kavita.Models.Entities.History;
using Kavita.Models.Entities.Scrobble;

namespace Kavita.API.Repositories;

public interface IScrobbleRepository
{
    void Attach(ScrobbleEvent evt);
    void Attach(ScrobbleError error);
    void Remove(ScrobbleEvent evt);
    void Remove(IEnumerable<ScrobbleEvent> events);
    void Remove(IEnumerable<ScrobbleError> errors);
    void Update(ScrobbleEvent evt);
    Task<IList<ScrobbleEvent>> GetByEvent(ScrobbleEventType type, bool isProcessed = false, CancellationToken ct = default);
    Task<IList<ScrobbleEvent>> GetProcessedEvents(int daysAgo, CancellationToken ct = default);
    Task<IEnumerable<ScrobbleErrorDto>> GetScrobbleErrors(CancellationToken ct = default);
    Task<IList<ScrobbleError>> GetAllScrobbleErrorsForSeries(int seriesId, CancellationToken ct = default);
    Task ClearScrobbleErrors(CancellationToken ct = default);
    Task<bool> HasErrorForSeries(int seriesId, CancellationToken ct = default);

    /// <summary>
    /// Get all events for a specific user and type
    /// </summary>
    /// <param name="scrobbleProvider"></param>
    /// <param name="userId"></param>
    /// <param name="seriesId"></param>
    /// <param name="chapterId"></param>
    /// <param name="eventType"></param>
    /// <param name="isNotProcessed">If true, only returned not processed events</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<ScrobbleEvent?> GetEvent(ScrobbleProvider scrobbleProvider, int userId, int seriesId, int? chapterId, ScrobbleEventType eventType, bool isNotProcessed = false, CancellationToken ct = default);
    Task<IEnumerable<ScrobbleEvent>> GetUserEventsForSeries(int userId, int seriesId, CancellationToken ct = default);

    /// <summary>
    /// Return the events with given ids, when belonging to the passed user
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="scrobbleEventIds"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<IList<ScrobbleEvent>> GetUserEvents(int userId, IList<long> scrobbleEventIds, CancellationToken ct = default);
    Task<PagedList<ScrobbleEventDto>> GetUserEvents(int userId, ScrobbleEventFilter filter, UserParams pagination, CancellationToken ct = default);
    Task<IList<ScrobbleEvent>> GetAllEventsForSeries(int seriesId, CancellationToken ct = default);
    Task<IList<ScrobbleEvent>> GetEvents(CancellationToken ct = default);
    /// <summary>
    /// Clears non-processed events for a given provider
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="provider"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task ClearEventsForProvider(int userId, ScrobbleProvider provider, CancellationToken ct = default);

    #region ScrobbleRuleHistory

    void AttachRuleHistory(ScrobbleRuleHistory row);

    /// <summary>
    /// Returns the tracked ledger row for the given key, or null. Used for the delivery-time upsert.
    /// </summary>
    Task<ScrobbleRuleHistory?> GetRuleHistory(int userId, ScrobbleProvider provider, TransitionRuleKind ruleKind, int seriesId, int? chapterId, CancellationToken ct = default);

    /// <summary>
    /// Returns all ledger rows for a user/provider/rule (no tracking). Used by the nightly guard.
    /// </summary>
    /// <remarks>No Tracking</remarks>
    Task<IList<ScrobbleRuleHistory>> GetRuleHistoryForProviderKind(int userId, ScrobbleProvider provider, TransitionRuleKind ruleKind, CancellationToken ct = default);

    /// <summary>
    /// Deletes ledger rows for a user where the user has read the series/chapter since the row was delivered.
    /// This is the read-reset: it lets a re-inactive series fire again without hooking the progress hot path.
    /// </summary>
    Task PurgeReadSinceDeliveryRuleHistory(int userId, CancellationToken ct = default);

    /// <summary>
    /// Deletes ledger rows for a user/provider/rule whose hash no longer matches the current configuration.
    /// Called when scrobble settings change so a re-configured rule re-evaluates every series.
    /// </summary>
    Task PurgeRuleHistoryByHashMismatch(int userId, ScrobbleProvider provider, TransitionRuleKind ruleKind, string currentHash, CancellationToken ct = default);

    /// <summary>
    /// Deletes all ledger rows for a user/provider. Called on provider disconnect.
    /// </summary>
    Task PurgeRuleHistoryForProvider(int userId, ScrobbleProvider provider, CancellationToken ct = default);

    /// <summary>
    /// Deletes queued (unprocessed) rule-generated events for a user/provider/rule so a changed rule does not
    /// deliver transitions computed under the old configuration.
    /// </summary>
    /// <param name="keepHash">When set, only events whose <see cref="ScrobbleEvent.RuleHashSnapshot"/> differs are
    /// deleted (config changed). When null, all queued events for the rule are deleted (rule disabled).</param>
    Task PurgeUnprocessedRuleEvents(int userId, ScrobbleProvider provider, TransitionRuleKind ruleKind, string? keepHash, CancellationToken ct = default);

    #endregion
}
