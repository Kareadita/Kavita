using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Kavita.API.Repositories;
using Kavita.Models.DTOs.KavitaPlus.Metadata;
using Kavita.Models.DTOs.Settings;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Metadata;
using Kavita.Models.Entities.MetadataMatching;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Repositories;


public class SettingsRepository(DataContext context, IMapper mapper) : ISettingsRepository
{
    public void Update(ServerSetting settings)
    {
        context.Entry(settings).State = EntityState.Modified;
    }

    public void Update(MetadataSettings settings)
    {
        context.Entry(settings).State = EntityState.Modified;
    }

    public void RemoveRange(List<MetadataFieldMapping> fieldMappings)
    {
        context.MetadataFieldMapping.RemoveRange(fieldMappings);
    }


    public async Task<MetadataSettings> GetMetadataSettings(CancellationToken ct = default)
    {
        return await context.MetadataSettings
            .Include(m => m.FieldMappings)
            .FirstAsync(ct);
    }

    public async Task<MetadataSettingsDto> GetMetadataSettingDto(CancellationToken ct = default)
    {
        return await context.MetadataSettings
            .Include(m => m.FieldMappings)
            .ProjectTo<MetadataSettingsDto>(mapper.ConfigurationProvider)
            .FirstAsync(ct);
    }

    public async Task<ServerSettingDto> GetSettingsDtoAsync(CancellationToken ct = default)
    {
        var settings = await context.ServerSetting
            .Select(x => x)
            .AsNoTracking()
            .ToListAsync(ct);
        return mapper.Map<ServerSettingDto>(settings);
    }

    public Task<ServerSetting> GetSettingAsync(ServerSettingKey key, CancellationToken ct = default)
    {
        return context.ServerSetting.SingleOrDefaultAsync(x => x.Key == key, ct)!;
    }

    public async Task<IEnumerable<ServerSetting>> GetSettingsAsync(CancellationToken ct = default)
    {
        return await context.ServerSetting.ToListAsync(ct);
    }
}
