using System.Collections.Generic;
using System.Threading;
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
    Task<bool> HasTokenExpired(string license, string token, ScrobbleProvider provider, CancellationToken ct = default);
    Task<int> GetRateLimit(string license, string token, CancellationToken ct = default);
    Task<ScrobbleResponseDto> PostScrobbleUpdate(ScrobbleDto data, string license, CancellationToken ct = default);
    Task<IList<MalStackDto>> GetMalStacks(string malUsername, string license, CancellationToken ct = default);
    Task<IList<ExternalSeriesMatchDto>> MatchSeries(MatchSeriesRequestDto request, CancellationToken ct = default);
    Task<SeriesDetailPlusApiDto> GetSeriesDetail(PlusSeriesRequestDto request, CancellationToken ct = default);
    Task<ExternalSeriesDetailDto> GetSeriesDetailById(ExternalMetadataIdsDto request, CancellationToken ct = default);
}
