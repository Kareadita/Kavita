using System.Collections.Generic;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Collection;
using Kavita.Models.DTOs.KavitaPlus.ExternalMetadata;
using Kavita.Models.DTOs.KavitaPlus.Metadata;
using Kavita.Models.DTOs.Metadata.Matching;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities.Enums;

namespace Kavita.API.Services.Plus;

/// <summary>
/// All Http requests to K+ should be contained in this service, the service will not handle any errors.
/// This is expected from the caller.
/// </summary>
public interface IKavitaPlusApiService
{
    Task<bool> HasTokenExpired(string license, string token, ScrobbleProvider provider);
    Task<int> GetRateLimit(string license, string token);
    Task<ScrobbleResponseDto> PostScrobbleUpdate(ScrobbleDto data, string license);
    Task<IList<MalStackDto>> GetMalStacks(string malUsername, string license);
    Task<IList<ExternalSeriesMatchDto>> MatchSeries(MatchSeriesRequestDto request);
    Task<SeriesDetailPlusApiDto> GetSeriesDetail(PlusSeriesRequestDto request);
    Task<ExternalSeriesDetailDto> GetSeriesDetailById(ExternalMetadataIdsDto request);
}
