using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Scrobbling;
using API.Entities;
using API.Entities.Scrobble;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface IScrobbleRepository
{
    void Attach(ScrobbleEvent evt);
    void Attach(ScrobbleError error);
    void Remove(ScrobbleEvent evt);
    Task<IList<ScrobbleEvent>> GetByEvent(ScrobbleEventType type);
    Task<bool> Exists(int userId, int seriesId, ScrobbleEventType eventType);
    Task<IEnumerable<ScrobbleErrorDto>> GetScrobbleErrors();
    Task ClearScrobbleErrors();
    Task<bool> HasErrorForSeries(int seriesId);
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

    public async Task<IList<ScrobbleEvent>> GetByEvent(ScrobbleEventType type)
    {
        return await _context.ScrobbleEvent
            .Include(s => s.Series)
            .ThenInclude(s => s.Library)
            .Include(s => s.AppUser)
            .Where(s => s.ScrobbleEventType == type)
            .AsSplitQuery()
            .GroupBy(s => s.SeriesId)
            .Select(g => g.OrderByDescending(e => e.ChapterNumber)
                .ThenByDescending(e => e.VolumeNumber)
                .FirstOrDefault())
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
}
