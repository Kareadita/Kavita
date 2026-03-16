using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.KavitaPlus.Metadata;
using Kavita.Models.DTOs.Settings;

namespace Kavita.API.Services;

public interface ISettingsService
{
    Task<MetadataSettingsDto> UpdateMetadataSettings(MetadataSettingsDto dto, CancellationToken ct = default);

    /// <summary>
    /// Update <see cref="MetadataSettings.Whitelist"/>, <see cref="MetadataSettings.Blacklist"/>, <see cref="MetadataSettings.AgeRatingMappings"/>, <see cref="MetadataSettings.FieldMappings"/>
    /// with data from the given dto.
    /// </summary>
    /// <param name="dto"></param>
    /// <param name="settings"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<FieldMappingsImportResultDto> ImportFieldMappings(FieldMappingsDto dto, ImportSettingsDto settings, CancellationToken ct = default);
    Task<ServerSettingDto> UpdateSettings(ServerSettingDto updateSettingsDto, CancellationToken ct = default);

    /// <summary>
    /// Check if the server can reach the authority at the given uri
    /// </summary>
    /// <param name="authority"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<bool> IsValidAuthority(string authority, CancellationToken ct = default);
}
