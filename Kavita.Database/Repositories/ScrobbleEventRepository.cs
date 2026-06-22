using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Kavita.API.Repositories;
using Kavita.Common.Helpers;
using Kavita.Database.Extensions;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.KavitaPlus;
using Kavita.Models.Entities.History;
using Kavita.Models.Entities.Scrobble;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Repositories;

/// <summary>
/// This handles everything around Scrobbling
/// </summary>
public class ScrobbleRepository(DataContext context, IMapper mapper) : IScrobbleRepository
{
    public void Attach(ScrobbleEvent evt)
    {
        context.ScrobbleEvent.Attach(evt);
    }

    public void Attach(ScrobbleError error)
    {
        context.ScrobbleError.Attach(error);
    }

    public void Remove(ScrobbleEvent evt)
    {
        context.ScrobbleEvent.Remove(evt);
    }

    public void Remove(IEnumerable<ScrobbleEvent> events)
    {
        context.ScrobbleEvent.RemoveRange(events);
    }

    public void Remove(IEnumerable<ScrobbleError> errors)
    {
        context.ScrobbleError.RemoveRange(errors);
    }

    public void Update(ScrobbleEvent evt)
    {
        context.Entry(evt).State = EntityState.Modified;
    }

    public async Task<IList<ScrobbleEvent>> GetByEvent(ScrobbleEventType type, bool isProcessed = false,
        CancellationToken ct = default)
    {
        return await context.ScrobbleEvent
            .Include(s => s.Series)
            .ThenInclude(s => s.Library)
            .Include(s => s.Series)
            .ThenInclude(s => s.Metadata)
            .Include(s => s.AppUser)
            .ThenInclude(u => u.UserPreferences)
            .Where(s => s.ScrobbleEventType == type)
            .Where(s => s.IsProcessed == isProcessed)
            .AsSplitQuery()
            .GroupBy(s => new {s.SeriesId, s.ScrobbleProvider})
            .Select(g => g.OrderByDescending(e => e.ChapterNumber)
                .ThenByDescending(e => e.VolumeNumber)
                .First())
            .ToListAsync(ct);
    }

    /// <summary>
    /// Returns all processed events processed 7 or more days ago
    /// </summary>
    /// <param name="daysAgo"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<IList<ScrobbleEvent>> GetProcessedEvents(int daysAgo, CancellationToken ct = default)
    {
        var date = DateTime.UtcNow.Subtract(TimeSpan.FromDays(daysAgo));
        return await context.ScrobbleEvent
            .Where(s => s.IsProcessed)
            .Where(s => s.ProcessDateUtc != null && s.ProcessDateUtc < date)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<ScrobbleErrorDto>> GetScrobbleErrors(CancellationToken ct = default)
    {
        return await context.ScrobbleError
            .OrderBy(e => e.LastModifiedUtc)
            .ProjectTo<ScrobbleErrorDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    public async Task<IList<ScrobbleError>> GetAllScrobbleErrorsForSeries(int seriesId, CancellationToken ct = default)
    {
        return await context.ScrobbleError
            .Where(e => e.SeriesId == seriesId)
            .ToListAsync(ct);
    }

    public async Task ClearScrobbleErrors(CancellationToken ct = default)
    {
        context.ScrobbleError.RemoveRange(context.ScrobbleError);
        await context.SaveChangesAsync(ct);
    }

    public async Task<bool> HasErrorForSeries(int seriesId, CancellationToken ct = default)
    {
        return await context.ScrobbleError.AnyAsync(n => n.SeriesId == seriesId, ct);
    }

    public async Task<ScrobbleEvent?> GetEvent(ScrobbleProvider scrobbleProvider, int userId, int seriesId,
        int? chapterId, ScrobbleEventType eventType,
        bool isNotProcessed = false, CancellationToken ct = default)
    {
        return await context.ScrobbleEvent
            .Where(e => e.ScrobbleProvider == scrobbleProvider
                        && e.AppUserId == userId
                        && e.SeriesId == seriesId
                        && e.ScrobbleEventType == eventType)
            .WhereIf(isNotProcessed, e => !e.IsProcessed)
            .WhereIf(chapterId.HasValue, e => e.ChapterId == chapterId)
            .OrderBy(e => e.LastModifiedUtc)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IEnumerable<ScrobbleEvent>> GetUserEventsForSeries(int userId, int seriesId,
        CancellationToken ct = default)
    {
        return await context.ScrobbleEvent
            .Where(e => e.AppUserId == userId && !e.IsProcessed && e.SeriesId == seriesId)
            .Include(e => e.Series)
            .OrderBy(e => e.LastModifiedUtc)
            .AsSplitQuery()
            .ToListAsync(ct);
    }

    public async Task<IList<ScrobbleEvent>> GetUserEvents(int userId, IList<long> scrobbleEventIds,
        CancellationToken ct = default)
    {
        return await context.ScrobbleEvent
            .Where(e => e.AppUserId == userId && scrobbleEventIds.Contains(e.Id))
            .ToListAsync(ct);
    }

    public async Task<PagedList<ScrobbleEventDto>> GetUserEvents(int userId, ScrobbleEventFilter filter,
        UserParams pagination, CancellationToken ct = default)
    {
        var query =  context.ScrobbleEvent
            .Where(e => e.AppUserId == userId)
            .Include(e => e.Series)
            .WhereIf(!string.IsNullOrEmpty(filter.Query), s =>
                EF.Functions.Like(s.Series.Name, $"%{filter.Query}%")
            )
            .WhereIf(!filter.IncludeReviews, e => e.ScrobbleEventType != ScrobbleEventType.Review)
            .SortBy(filter.Field, filter.IsDescending)
            .AsSplitQuery()
            .ProjectTo<ScrobbleEventDto>(mapper.ConfigurationProvider);

        return await PagedList<ScrobbleEventDto>.CreateAsync(query, pagination.PageNumber, pagination.PageSize, ct);
    }

    public async Task<IList<ScrobbleEvent>> GetAllEventsForSeries(int seriesId, CancellationToken ct = default)
    {
        return await context.ScrobbleEvent
            .Where(e => e.SeriesId == seriesId)
            .ToListAsync(ct);
    }

    public async Task<IList<ScrobbleEvent>> GetEvents(CancellationToken ct = default)
    {
        return await context.ScrobbleEvent
            .Include(e => e.AppUser)
            .ToListAsync(ct);
    }

    public Task ClearEventsForProvider(int userId, ScrobbleProvider provider, CancellationToken ct = default)
    {
        return context.ScrobbleEvent
            .Where(e => e.AppUserId == userId && e.ScrobbleProvider == provider)
            .ExecuteDeleteAsync(ct);
    }

    #region ScrobbleRuleHistory

    public void AttachRuleHistory(ScrobbleRuleHistory row)
    {
        context.ScrobbleRuleHistory.Attach(row);
    }

    public Task<ScrobbleRuleHistory?> GetRuleHistory(int userId, ScrobbleProvider provider, TransitionRuleKind ruleKind,
        int seriesId, int? chapterId, CancellationToken ct = default)
    {
        return context.ScrobbleRuleHistory
            .FirstOrDefaultAsync(h => h.AppUserId == userId && h.Provider == provider && h.RuleKind == ruleKind
                                      && h.SeriesId == seriesId && h.ChapterId == chapterId, ct);
    }

    public async Task<IList<ScrobbleRuleHistory>> GetRuleHistoryForProviderKind(int userId, ScrobbleProvider provider,
        TransitionRuleKind ruleKind, CancellationToken ct = default)
    {
        return await context.ScrobbleRuleHistory
            .Where(h => h.AppUserId == userId && h.Provider == provider && h.RuleKind == ruleKind)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public Task PurgeReadSinceDeliveryRuleHistory(int userId, CancellationToken ct = default)
    {
        return context.ScrobbleRuleHistory
            .Where(h => h.AppUserId == userId)
            .Where(h => context.AppUserProgresses.Any(p => p.AppUserId == userId && p.SeriesId == h.SeriesId
                        && (h.ChapterId == null || p.ChapterId == h.ChapterId)
                        && p.LastModifiedUtc > h.CreatedUtc))
            .ExecuteDeleteAsync(ct);
    }

    public Task PurgeRuleHistoryByHashMismatch(int userId, ScrobbleProvider provider, TransitionRuleKind ruleKind,
        string currentHash, CancellationToken ct = default)
    {
        return context.ScrobbleRuleHistory
            .Where(h => h.AppUserId == userId && h.Provider == provider && h.RuleKind == ruleKind
                        && h.RuleHash != currentHash)
            .ExecuteDeleteAsync(ct);
    }

    public Task PurgeRuleHistoryForProvider(int userId, ScrobbleProvider provider, CancellationToken ct = default)
    {
        return context.ScrobbleRuleHistory
            .Where(h => h.AppUserId == userId && h.Provider == provider)
            .ExecuteDeleteAsync(ct);
    }

    public Task PurgeUnprocessedRuleEvents(int userId, ScrobbleProvider provider, TransitionRuleKind ruleKind,
        string? keepHash, CancellationToken ct = default)
    {
        var query = context.ScrobbleEvent
            .Where(e => e.AppUserId == userId && e.ScrobbleProvider == provider
                        && e.TransitionRuleKind == ruleKind && !e.IsProcessed);

        // keepHash set => config changed, drop only the now-stale events; null => rule disabled, drop them all
        if (keepHash != null)
        {
            query = query.Where(e => e.RuleHashSnapshot != keepHash);
        }

        return query.ExecuteDeleteAsync(ct);
    }

    #endregion
}
