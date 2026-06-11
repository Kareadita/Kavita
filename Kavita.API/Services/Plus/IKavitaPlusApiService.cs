using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Collection;
using Kavita.Models.DTOs.KavitaPlus;
using Kavita.Models.DTOs.KavitaPlus.ExternalMetadata;
using Kavita.Models.DTOs.KavitaPlus.ExternalMetadata.Covers;
using Kavita.Models.DTOs.KavitaPlus.License;
using Kavita.Models.DTOs.KavitaPlus.Metadata;
using Kavita.Models.DTOs.KavitaPlus.Scrobble;
using Kavita.Models.DTOs.Metadata.Matching;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities.Enums;

namespace Kavita.API.Services.Plus;

/// <summary>
/// All Http requests to K+ should be contained in this service, the service will not handle any errors.
/// This is expected from the caller.
///
/// Methods returning <see cref="KPlusResult{T}"/> will NOT thrown.
/// </summary>
public interface IKavitaPlusApiService
{
    [Obsolete]
    Task<int> GetRateLimitAsync(string license, string token, CancellationToken ct = default);
    [Obsolete]
    Task<IList<MalStackDto>> GetMalStacksAsync(string malUsername, string license, CancellationToken ct = default);
    [Obsolete]
    Task<IList<ExternalSeriesMatchDto>> MatchSeriesAsync(MatchSeriesRequestDto request, CancellationToken ct = default);
    [Obsolete]
    Task<SeriesDetailPlusApiDto> GetSeriesDetailAsync(PlusSeriesRequestDto request, CancellationToken ct = default);
    [Obsolete]
    Task<ExternalSeriesDetailDto> GetSeriesDetailByIdAsync(ExternalMetadataIdsDto request, CancellationToken ct = default);

    Task<KPlusResult<SeriesDetailPlusApiDto?>> GetSeriesDetailV3Async(SeriesDetailRequestV3Dto request, CancellationToken ct = default);
    Task<KPlusResult<List<ExternalSeriesMatchDto>>> MatchSeriesV3Async(MatchRequestV3Dto request, CancellationToken ct = default);
    Task<ScrobbleResponseDto> PostScrobbleV3UpdateAsync(ScrobbleV3Dto data, string license, CancellationToken ct = default);
    Task<KPlusResult<bool>> HasTokenExpiredForProviderAsync(ScrobbleProvider provider, string token, string license, CancellationToken ct = default);
    Task<KPlusResult<int>> GetRateLimitForProviderAsync(ScrobbleProvider provider, string token, string license, CancellationToken ct = default);
    Task<KPlusResult<IList<ExternalCoverResponseDto>>> GetCoverImagesAsync(ExternalCoverRequestDto request, CancellationToken ct = default);
    Task<KPlusResult<List<ExternalSeriesDetailDto>>> GetWantToRead(ScrobbleProvider provider, string token, string license, CancellationToken ct = default);
    Task<KPlusResult<KavitaPlusUserInfo>> GetUserInfo(ScrobbleProvider provider, string token, string license, CancellationToken ct = default);
    Task<LicenseInfoDto?> GetLicenseInfo(CancellationToken ct = default);
    Task<IList<KavitaPlusProviderHealthSnapshotDto>> GetProviderHealthSnapshot(CancellationToken ct = default);
    Task<KavitaPlusLicenseUsageDto> GetLicenseUsage(CancellationToken ct = default);
}
