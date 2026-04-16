using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.KavitaPlus.Metadata;
using Kavita.Models.DTOs.Settings;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Metadata;
using Kavita.Models.Entities.MetadataMatching;

namespace Kavita.API.Repositories;

public interface ISettingsRepository
{
    void Update(ServerSetting settings);
    void Update(MetadataSettings settings);
    void RemoveRange(List<MetadataFieldMapping> fieldMappings);
    Task<ServerSettingDto> GetSettingsDtoAsync(CancellationToken ct = default);
    Task<ServerSetting> GetSettingAsync(ServerSettingKey key, CancellationToken ct = default);
    Task<IEnumerable<ServerSetting>> GetSettingsAsync(CancellationToken ct = default);
    Task<MetadataSettings> GetMetadataSettings(CancellationToken ct = default);
    Task<MetadataSettingsDto> GetMetadataSettingDto(CancellationToken ct = default);
}
