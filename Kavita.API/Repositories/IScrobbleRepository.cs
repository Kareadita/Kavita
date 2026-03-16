using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs.Scrobbling;
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
    Task<bool> Exists(int userId, int seriesId, ScrobbleEventType eventType, CancellationToken ct = default);
    Task<IEnumerable<ScrobbleErrorDto>> GetScrobbleErrors(CancellationToken ct = default);
    Task<IList<ScrobbleError>> GetAllScrobbleErrorsForSeries(int seriesId, CancellationToken ct = default);
    Task ClearScrobbleErrors(CancellationToken ct = default);
    Task<bool> HasErrorForSeries(int seriesId, CancellationToken ct = default);

    /// <summary>
    /// Get all events for a specific user and type
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="seriesId"></param>
    /// <param name="eventType"></param>
    /// <param name="isNotProcessed">If true, only returned not processed events</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<ScrobbleEvent?> GetEvent(int userId, int seriesId, ScrobbleEventType eventType, bool isNotProcessed = false, CancellationToken ct = default);
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
    Task<IList<ScrobbleEvent>> GetAllEventsWithSeriesIds(IEnumerable<int> seriesIds, CancellationToken ct = default);
    Task<IList<ScrobbleEvent>> GetEvents(CancellationToken ct = default);
}
