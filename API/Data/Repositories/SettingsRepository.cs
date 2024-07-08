using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.SeriesDetail;
using API.DTOs.Settings;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface ISettingsRepository
{
    void Update(ServerSetting settings);
    Task<ServerSettingDto> GetSettingsDtoAsync();
    Task<ServerSetting> GetSettingAsync(ServerSettingKey key);
    Task<IEnumerable<ServerSetting>> GetSettingsAsync();
    void Remove(ServerSetting setting);
    Task<ExternalSeriesMetadata?> GetExternalSeriesMetadata(int seriesId);
}
public class SettingsRepository : ISettingsRepository
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;

    public SettingsRepository(DataContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public void Update(ServerSetting settings)
    {
        _context.Entry(settings).State = EntityState.Modified;
    }

    public void Remove(ServerSetting setting)
    {
        _context.Remove(setting);
    }

    public async Task<ExternalSeriesMetadata?> GetExternalSeriesMetadata(int seriesId)
    {
        return await _context.ExternalSeriesMetadata
            .Where(s => s.SeriesId == seriesId)
            .FirstOrDefaultAsync();
    }

    public async Task<ServerSettingDto> GetSettingsDtoAsync()
    {
        var settings = await _context.ServerSetting
            .Select(x => x)
            .AsNoTracking()
            .ToListAsync();
        return _mapper.Map<ServerSettingDto>(settings);
    }

    public Task<ServerSetting> GetSettingAsync(ServerSettingKey key)
    {
        return _context.ServerSetting.SingleOrDefaultAsync(x => x.Key == key)!;
    }

    public async Task<IEnumerable<ServerSetting>> GetSettingsAsync()
    {
        return await _context.ServerSetting.ToListAsync();
    }
}
