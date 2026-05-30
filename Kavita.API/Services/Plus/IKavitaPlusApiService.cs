using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Collection;
using Kavita.Models.DTOs.KavitaPlus;
using Kavita.Models.DTOs.KavitaPlus.ExternalMetadata;
using Kavita.Models.DTOs.KavitaPlus.ExternalMetadata.Covers;
using Kavita.Models.DTOs.KavitaPlus.License;
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
    Task<bool> HasTokenExpiredAsync(string license, string token, ScrobbleProvider provider, CancellationToken ct = default);
    Task<int> GetRateLimitAsync(string license, string token, CancellationToken ct = default);
    Task<ScrobbleResponseDto> PostScrobbleUpdateAsync(ScrobbleDto data, string license, CancellationToken ct = default);
    Task<IList<MalStackDto>> GetMalStacksAsync(string malUsername, string license, CancellationToken ct = default);
    Task<IList<ExternalSeriesMatchDto>> MatchSeriesAsync(MatchSeriesRequestDto request, CancellationToken ct = default);
    Task<SeriesDetailPlusApiDto> GetSeriesDetailAsync(PlusSeriesRequestDto request, CancellationToken ct = default);
    Task<ExternalSeriesDetailDto> GetSeriesDetailByIdAsync(ExternalMetadataIdsDto request, CancellationToken ct = default);
    Task<KPlusResult<IList<ExternalCoverResponseDto>>> GetCoverImagesAsync(ExternalCoverRequestDto request, CancellationToken ct = default);
    Task<LicenseInfoDto?> GetLicenseInfo(CancellationToken ct = default);
    Task<IList<KavitaPlusProviderHealthSnapshotDto>> GetProviderHealthSnapshot(CancellationToken ct = default);
    Task<KavitaPlusLicenseUsageDto> GetLicenseUsage(CancellationToken ct = default);
}
