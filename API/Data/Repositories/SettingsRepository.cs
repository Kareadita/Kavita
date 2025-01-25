using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.KavitaPlus.Metadata;
using API.DTOs.SeriesDetail;
using API.DTOs.Settings;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface ISettingsRepository
{
    void Update(ServerSetting settings);
    void Update(MetadataSettings settings);
    void RemoveRange(List<MetadataFieldMapping> fieldMappings);
    Task<ServerSettingDto> GetSettingsDtoAsync();
    Task<ServerSetting> GetSettingAsync(ServerSettingKey key);
    Task<IEnumerable<ServerSetting>> GetSettingsAsync();
    void Remove(ServerSetting setting);
    Task<ExternalSeriesMetadata?> GetExternalSeriesMetadata(int seriesId);
    Task<MetadataSettings> GetMetadataSettings();
    Task<MetadataSettingsDto> GetMetadataSettingDto();
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

    public void Update(MetadataSettings settings)
    {
        _context.Entry(settings).State = EntityState.Modified;
    }

    public void RemoveRange(List<MetadataFieldMapping> fieldMappings)
    {
        _context.MetadataFieldMapping.RemoveRange(fieldMappings);
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

    public async Task<MetadataSettings> GetMetadataSettings()
    {
        return await _context.MetadataSettings
            .Include(m => m.FieldMappings)
            .FirstAsync();
    }

    public async Task<MetadataSettingsDto> GetMetadataSettingDto()
    {
        return await _context.MetadataSettings
            .Include(m => m.FieldMappings)
            .ProjectTo<MetadataSettingsDto>(_mapper.ConfigurationProvider)
            .FirstAsync();
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
