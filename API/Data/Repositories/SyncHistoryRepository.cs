using System;
using System.Linq;
using System.Threading.Tasks;
using API.Entities.Enums;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface ISyncHistoryRepository
{
    Task<DateTime?> GetSyncTime(SyncKey key);
}

public class SyncHistoryRepository : ISyncHistoryRepository
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;

    public SyncHistoryRepository(DataContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<DateTime?> GetSyncTime(SyncKey key)
    {
        return await _context.SyncHistory
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();
    }
}
