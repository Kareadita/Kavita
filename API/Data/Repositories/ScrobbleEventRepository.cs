using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Scrobbling;
using API.Entities;
using API.Entities.Scrobble;
using API.Extensions.QueryExtensions;
using API.Helpers;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface IScrobbleRepository
{
    void Attach(ScrobbleEvent evt);
    void Attach(ScrobbleError error);
    void Remove(ScrobbleEvent evt);
    void Remove(IEnumerable<ScrobbleEvent> events);
    void Update(ScrobbleEvent evt);
    Task<IList<ScrobbleEvent>> GetByEvent(ScrobbleEventType type, bool isProcessed = false);
    Task<IList<ScrobbleEvent>> GetProcessedEvents(int daysAgo);
    Task<bool> Exists(int userId, int seriesId, ScrobbleEventType eventType);
    Task<IEnumerable<ScrobbleErrorDto>> GetScrobbleErrors();
    Task ClearScrobbleErrors();
    Task<bool> HasErrorForSeries(int seriesId);
    Task<ScrobbleEvent?> GetEvent(int userId, int seriesId, ScrobbleEventType eventType);
    Task<IEnumerable<ScrobbleEvent>> GetUserEventsForSeries(int userId, int seriesId);
    Task<PagedList<ScrobbleEventDto>> GetUserEvents(int userId, ScrobbleEventFilter filter, UserParams pagination);
    Task<IList<ScrobbleEvent>> GetAllEventsForSeries(int seriesId);
}

/// <summary>
/// This handles everything around Scrobbling
/// </summary>
public class ScrobbleRepository : IScrobbleRepository
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;

    public ScrobbleRepository(DataContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public void Attach(ScrobbleEvent evt)
    {
        _context.ScrobbleEvent.Attach(evt);
    }

    public void Attach(ScrobbleError error)
    {
        _context.ScrobbleError.Attach(error);
    }

    public void Remove(ScrobbleEvent evt)
    {
        _context.ScrobbleEvent.Remove(evt);
    }

    public void Remove(IEnumerable<ScrobbleEvent> events)
    {
        _context.ScrobbleEvent.RemoveRange(events);
    }

    public void Update(ScrobbleEvent evt)
    {
        _context.Entry(evt).State = EntityState.Modified;
    }

    public async Task<IList<ScrobbleEvent>> GetByEvent(ScrobbleEventType type, bool isProcessed = false)
    {
        return await _context.ScrobbleEvent
            .Include(s => s.Series)
            .ThenInclude(s => s.Library)
            .Include(s => s.Series)
            .ThenInclude(s => s.Metadata)
            .Include(s => s.AppUser)
            .ThenInclude(u => u.UserPreferences)
            .Where(s => s.ScrobbleEventType == type)
            .Where(s => s.IsProcessed == isProcessed)
            .AsSplitQuery()
            .GroupBy(s => s.SeriesId)
            .Select(g => g.OrderByDescending(e => e.ChapterNumber)
                .ThenByDescending(e => e.VolumeNumber)
                .FirstOrDefault())
            .ToListAsync();
    }

    public async Task<IList<ScrobbleEvent>> GetProcessedEvents(int daysAgo)
    {
        var date = DateTime.UtcNow.Subtract(TimeSpan.FromDays(daysAgo));
        return await _context.ScrobbleEvent
            .Where(s => s.IsProcessed)
            .Where(s => s.ProcessDateUtc != null && s.ProcessDateUtc < date)
            .ToListAsync();
    }

    public async Task<bool> Exists(int userId, int seriesId, ScrobbleEventType eventType)
    {
        return await _context.ScrobbleEvent.AnyAsync(e =>
            e.AppUserId == userId && e.SeriesId == seriesId && e.ScrobbleEventType == eventType);
    }

    public async Task<IEnumerable<ScrobbleErrorDto>> GetScrobbleErrors()
    {
        return await _context.ScrobbleError
            .OrderBy(e => e.LastModifiedUtc)
            .ProjectTo<ScrobbleErrorDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task ClearScrobbleErrors()
    {
        _context.ScrobbleError.RemoveRange(_context.ScrobbleError);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> HasErrorForSeries(int seriesId)
    {
        return await _context.ScrobbleError.AnyAsync(n => n.SeriesId == seriesId);
    }

    public async Task<ScrobbleEvent?> GetEvent(int userId, int seriesId, ScrobbleEventType eventType)
    {
        return await _context.ScrobbleEvent.FirstOrDefaultAsync(e =>
            e.AppUserId == userId && e.SeriesId == seriesId && e.ScrobbleEventType == eventType);
    }

    public async Task<IEnumerable<ScrobbleEvent>> GetUserEventsForSeries(int userId, int seriesId)
    {
        return await _context.ScrobbleEvent
            .Where(e => e.AppUserId == userId && !e.IsProcessed)
            .Include(e => e.Series)
            .OrderBy(e => e.LastModifiedUtc)
            .AsSplitQuery()
            .ToListAsync();
    }

    public async Task<PagedList<ScrobbleEventDto>> GetUserEvents(int userId, ScrobbleEventFilter filter, UserParams pagination)
    {
        var query =  _context.ScrobbleEvent
            .Where(e => e.AppUserId == userId)
            .Include(e => e.Series)
            .SortBy(filter.Field, filter.IsDescending)
            .WhereIf(!string.IsNullOrEmpty(filter.Query), s =>
                EF.Functions.Like(s.Series.Name, $"%{filter.Query}%")
            )
            .WhereIf(!filter.IncludeReviews, e => e.ScrobbleEventType != ScrobbleEventType.Review)
            .AsSplitQuery()
            .ProjectTo<ScrobbleEventDto>(_mapper.ConfigurationProvider);

        return await PagedList<ScrobbleEventDto>.CreateAsync(query, pagination.PageNumber, pagination.PageSize);
    }

    public async Task<IList<ScrobbleEvent>> GetAllEventsForSeries(int seriesId)
    {
        return await _context.ScrobbleEvent.Where(e => e.SeriesId == seriesId)
            .ToListAsync();
    }
}
