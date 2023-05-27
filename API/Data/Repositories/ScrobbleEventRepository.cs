using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Scrobbling;
using API.Entities;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface IScrobbleEventRepository
{
    void Attach(ScrobbleEvent evt);
    void Remove(ScrobbleEvent evt);
    Task<IList<ScrobbleEvent>> GetByEvent(ScrobbleEventType type);
    Task<bool> Exists(int userId, int seriesId, ScrobbleEventType eventType);
}

public class ScrobbleEventRepository : IScrobbleEventRepository
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;

    public ScrobbleEventRepository(DataContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public void Attach(ScrobbleEvent evt)
    {
        _context.ScrobbleEvent.Attach(evt);
    }

    public void Remove(ScrobbleEvent evt)
    {
        _context.ScrobbleEvent.Remove(evt);
    }

    public async Task<IList<ScrobbleEvent>> GetByEvent(ScrobbleEventType type)
    {
        return await _context.ScrobbleEvent
            .Include(s => s.Series)
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
}
