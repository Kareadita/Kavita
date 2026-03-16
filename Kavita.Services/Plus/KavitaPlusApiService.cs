#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http;
using Kavita.API.Database;
using Kavita.API.Services.Plus;
using Kavita.Common;
using Kavita.Common.Extensions;
using Kavita.Models.DTOs.Collection;
using Kavita.Models.DTOs.KavitaPlus.ExternalMetadata;
using Kavita.Models.DTOs.KavitaPlus.Metadata;
using Kavita.Models.DTOs.Metadata.Matching;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities.Enums;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Plus;

public class KavitaPlusApiService(ILogger<KavitaPlusApiService> logger, IUnitOfWork unitOfWork): IKavitaPlusApiService
{
    private const string ScrobblingPath = "/api/scrobbling/";

    public async Task<bool> HasTokenExpired(string license, string token, ScrobbleProvider provider,
        CancellationToken ct = default)
    {
        var res = await Get(ScrobblingPath + "valid-key?provider=" + provider + "&key=" + token, license, token);
        var str = await res.GetStringAsync();
        return bool.Parse(str);
    }

    public async Task<int> GetRateLimit(string license, string token, CancellationToken ct = default)
    {
        var res = await Get(ScrobblingPath + "rate-limit?accessToken=" + token, license, token);
        var str = await res.GetStringAsync();
        return int.Parse(str);
    }

    public async Task<ScrobbleResponseDto> PostScrobbleUpdate(ScrobbleDto data, string license,
        CancellationToken ct = default)
    {
        return await PostAndReceive<ScrobbleResponseDto>(ScrobblingPath + "update", data, license);
    }

    public async Task<IList<MalStackDto>> GetMalStacks(string malUsername, string license, CancellationToken ct = default)
    {
        return await $"{Configuration.KavitaPlusApiUrl}/api/metadata/v2/stacks?username={malUsername}"
            .WithKavitaPlusHeaders(license)
            .GetJsonAsync<IList<MalStackDto>>(cancellationToken: ct);
    }

    public async Task<IList<ExternalSeriesMatchDto>> MatchSeries(MatchSeriesRequestDto request,
        CancellationToken ct = default)
    {
        var license = (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct)).Value;
        var token = (await unitOfWork.UserRepository.GetDefaultAdminUser(ct: ct)).AniListAccessToken;

        return await (Configuration.KavitaPlusApiUrl + "/api/metadata/v2/match-series")
            .WithKavitaPlusHeaders(license, token)
            .PostJsonAsync(request, cancellationToken: ct)
            .ReceiveJson<IList<ExternalSeriesMatchDto>>();
    }

    public async Task<SeriesDetailPlusApiDto> GetSeriesDetail(PlusSeriesRequestDto request, CancellationToken ct = default)
    {
        var license = (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct)).Value;
        var token = (await unitOfWork.UserRepository.GetDefaultAdminUser(ct: ct)).AniListAccessToken;

        return await (Configuration.KavitaPlusApiUrl + "/api/metadata/v2/series-detail")
            .WithKavitaPlusHeaders(license, token)
            .PostJsonAsync(request, cancellationToken: ct)
            .ReceiveJson<SeriesDetailPlusApiDto>();
    }

    public async Task<ExternalSeriesDetailDto> GetSeriesDetailById(ExternalMetadataIdsDto request,
        CancellationToken ct = default)
    {
        var license = (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct)).Value;
        var token = (await unitOfWork.UserRepository.GetDefaultAdminUser(ct: ct)).AniListAccessToken;

        return await (Configuration.KavitaPlusApiUrl + "/api/metadata/v2/series-by-ids")
            .WithKavitaPlusHeaders(license, token)
            .PostJsonAsync(request, cancellationToken: ct)
            .ReceiveJson<ExternalSeriesDetailDto>();
    }

    /// <summary>
    /// Send a GET request to K+
    /// </summary>
    /// <param name="url">only path of the uri, the host is added</param>
    /// <param name="license"></param>
    /// <param name="aniListToken"></param>
    /// <returns></returns>
    private static async Task<IFlurlResponse> Get(string url, string license, string? aniListToken = null)
    {
        return await (Configuration.KavitaPlusApiUrl + url)
            .WithKavitaPlusHeaders(license, aniListToken)
            .GetAsync();
    }

    /// <summary>
    /// Send a POST request to K+
    /// </summary>
    /// <param name="url">only path of the uri, the host is added</param>
    /// <param name="body"></param>
    /// <param name="license"></param>
    /// <param name="aniListToken"></param>
    /// <typeparam name="T">Return type</typeparam>
    /// <returns></returns>
    private static async Task<T> PostAndReceive<T>(string url, object body, string license, string? aniListToken = null)
    {
        return await (Configuration.KavitaPlusApiUrl + url)
            .WithKavitaPlusHeaders(license, aniListToken)
            .PostJsonAsync(body)
            .ReceiveJson<T>();
    }
}
