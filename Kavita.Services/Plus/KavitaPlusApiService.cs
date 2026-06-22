#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Flurl;
using Flurl.Http;
using Kavita.API.Database;
using Kavita.API.Services.Plus;
using Kavita.Common;
using Kavita.Common.Extensions;
using Kavita.Models.DTOs.Collection;
using Kavita.Models.DTOs.KavitaPlus;
using Kavita.Models.DTOs.KavitaPlus.ExternalMetadata;
using Kavita.Models.DTOs.KavitaPlus.ExternalMetadata.Covers;
using Kavita.Models.DTOs.KavitaPlus.License;
using Kavita.Models.DTOs.KavitaPlus.Metadata;
using Kavita.Models.DTOs.KavitaPlus.OAuth;
using Kavita.Models.DTOs.KavitaPlus.Scrobble;
using Kavita.Models.DTOs.Metadata.Matching;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities.Enums;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Plus;

public class KavitaPlusApiService(ILogger<KavitaPlusApiService> logger, IUnitOfWork unitOfWork, IDataProtectionProvider dataProtectionProvider): IKavitaPlusApiService
{
    private const string ScrobblingPath = "/api/scrobbling/";
    public const string ApiKeyDataProtectorName = "KavitaPlus.ApiKey";

    private readonly IDataProtector _dataProtector = dataProtectionProvider.CreateProtector(ApiKeyDataProtectorName);

    public async Task<int> GetRateLimitAsync(string license, string token, CancellationToken ct = default)
    {
        var res = await Get(ScrobblingPath + "rate-limit?accessToken=" + token, license, token);
        var str = await res.GetStringAsync();
        return int.Parse(str);
    }


    public async Task<IList<MalStackDto>> GetMalStacksAsync(string malUsername, string license, CancellationToken ct = default)
    {
        return await $"{Configuration.KavitaPlusApiUrl}/api/metadata/v2/stacks?username={malUsername}"
            .WithKavitaPlusHeaders(license)
            .GetJsonAsync<IList<MalStackDto>>(cancellationToken: ct);
    }

    public async Task<IList<ExternalSeriesMatchDto>> MatchSeriesAsync(MatchSeriesRequestDto request,
        CancellationToken ct = default)
    {
        var license = (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct)).Value;
        var token = (await unitOfWork.UserRepository.GetDefaultAdminUser(ct: ct))
            .ScrobbleProviders[ScrobbleProvider.AniList]
            .AuthenticationToken;

        return await (Configuration.KavitaPlusApiUrl + "/api/metadata/v2/match-series")
            .WithKavitaPlusHeaders(license, token)
            .PostJsonAsync(request, cancellationToken: ct)
            .ReceiveJson<IList<ExternalSeriesMatchDto>>();
    }

    public async Task<SeriesDetailPlusApiDto> GetSeriesDetailAsync(PlusSeriesRequestDto request, CancellationToken ct = default)
    {
        var license = (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct)).Value;
        var token = (await unitOfWork.UserRepository.GetDefaultAdminUser(ct: ct))
            .ScrobbleProviders[ScrobbleProvider.AniList]
            .AuthenticationToken;

        return await (Configuration.KavitaPlusApiUrl + "/api/metadata/v2/series-detail")
            .WithKavitaPlusHeaders(license, token)
            .PostJsonAsync(request, cancellationToken: ct)
            .ReceiveJson<SeriesDetailPlusApiDto>();
    }

    public async Task<ExternalSeriesDetailDto> GetSeriesDetailByIdAsync(ExternalMetadataIdsDto request,
        CancellationToken ct = default)
    {
        var license = (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct)).Value;
        var token = (await unitOfWork.UserRepository.GetDefaultAdminUser(ct: ct))
            .ScrobbleProviders[ScrobbleProvider.AniList]
            .AuthenticationToken;

        return await (Configuration.KavitaPlusApiUrl + "/api/metadata/v2/series-by-ids")
            .WithKavitaPlusHeaders(license, token)
            .PostJsonAsync(request, cancellationToken: ct)
            .ReceiveJson<ExternalSeriesDetailDto>();
    }

    public async Task<KPlusResult<SeriesDetailPlusApiDto?>> GetSeriesDetailV3Async(SeriesDetailRequestV3Dto request, CancellationToken ct = default)
    {
        try
        {
            var license = (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct)).Value;

            return await (Configuration.KavitaPlusApiUrl + "/api/v3/Metadata/series-detail")
                .WithKavitaPlusHeaders(license)
                .PostJsonAsync(request, cancellationToken: ct)
                .ReceiveJson<KPlusResult<SeriesDetailPlusApiDto?>>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an issue getting series detail from Kavita+ for Series ({SeriesName})", request.SeriesName);
            return KPlusResult<SeriesDetailPlusApiDto?>.Failure(ex.Message);
        }
    }

    public async Task<KPlusResult<List<ExternalSeriesMatchDto>>> MatchSeriesV3Async(MatchRequestV3Dto request, CancellationToken ct = default)
    {
        try
        {
            var license = (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct)).Value;

            return await (Configuration.KavitaPlusApiUrl + "/api/v3/Metadata/match")
                .WithKavitaPlusHeaders(license)
                .PostJsonAsync(request, cancellationToken: ct)
                .ReceiveJson<KPlusResult<List<ExternalSeriesMatchDto>>>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an issue matching series from Kavita+ for Series ({SeriesName})", request.SeriesName);
            return KPlusResult<List<ExternalSeriesMatchDto>>.Failure(ex.Message);
        }
    }

    public async Task<ScrobbleResponseDto> PostScrobbleV3UpdateAsync(ScrobbleV3Dto data, string license, CancellationToken ct = default)
    {
        try
        {
            return await (Configuration.KavitaPlusApiUrl + "/api/v3/Scrobble")
                .WithKavitaPlusHeaders(license)
                .PostJsonAsync(data, cancellationToken: ct)
                .ReceiveJson<ScrobbleResponseDto>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an issue posting scrobble to Kavita+ for provider {Provider}", data.Provider);
            return new ScrobbleResponseDto
            {
                ErrorMessage = ex.Message,
                Successful = false
            };
        }
    }

    public async Task<KPlusResult<bool>> HasTokenExpiredForProviderAsync(ScrobbleProvider provider, string token, string license, CancellationToken ct = default)
    {
        try
        {
            return await (Configuration.KavitaPlusApiUrl + "/api/v3/Scrobble/valid-access-token")
                .WithKavitaPlusHeaders(license)
                .SetQueryParam("provider", provider)
                .SetQueryParam("accessToken", token)
                .GetJsonAsync<KPlusResult<bool>>(cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an issue getting token validity from Kavita+ for provider {Provider}", provider);
            return KPlusResult<bool>.Failure(ex.Message);
        }
    }

    public async Task<KPlusResult<int>> GetRateLimitForProviderAsync(ScrobbleProvider provider, string token, string license, CancellationToken ct = default)
    {
        try
        {
            return await (Configuration.KavitaPlusApiUrl + "/api/v3/Scrobble/rate-limit")
                .WithKavitaPlusHeaders(license)
                .SetQueryParam("provider", provider)
                .SetQueryParam("accessToken", token)
                .GetJsonAsync<KPlusResult<int>>(cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an issue getting rate limit from Kavita+ for provider {Provider}", provider);
            return KPlusResult<int>.Failure(ex.Message);
        }
    }

    public async Task<KPlusResult<IList<ExternalCoverResponseDto>>> GetCoverImagesAsync(ExternalCoverRequestDto request,
        CancellationToken ct = default)
    {
        try
        {
            var license = (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct)).Value;
            var token = (await unitOfWork.UserRepository.GetDefaultAdminUser(ct: ct))
                .ScrobbleProviders[ScrobbleProvider.AniList]
                .AuthenticationToken;

            return await (Configuration.KavitaPlusApiUrl + "/api/v3/metadata/covers")
                .WithKavitaPlusHeaders(license, token)
                .PostJsonAsync(request, cancellationToken: ct)
                .ReceiveJson<KPlusResult<IList<ExternalCoverResponseDto>>>();
        }
        catch (Exception ex)
        {
            // TODO: How should I handle this? swallow and return nothing
            logger.LogError(ex, "There was an issue getting cover images from Kavita+ for Series ({SeriesName})", request.SeriesName);
            return KPlusResult<IList<ExternalCoverResponseDto>>.Failure(ex.Message);
        }
    }

    public async Task<KPlusResult<List<ExternalSeriesDetailDto>>> GetWantToRead(ScrobbleProvider provider, string token,
        string license, CancellationToken ct = default)
    {
        try
        {
            return await (Configuration.KavitaPlusApiUrl + "/api/v3/Scrobble/want-to-read")
                .WithKavitaPlusHeaders(license)
                .WithTimeout(TimeSpan.FromSeconds(120))
                .SetQueryParam("provider", provider)
                .SetQueryParam("accessToken", token)
                .GetJsonAsync<KPlusResult<List<ExternalSeriesDetailDto>>>(cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an issue getting want to read from Kavita+ for provider {Provider}", provider);
            return KPlusResult<List<ExternalSeriesDetailDto>>.Failure(ex.Message);
        }
    }

    public async Task<KPlusResult<KavitaPlusUserInfo>> GetUserInfo(ScrobbleProvider provider, string token, string license, CancellationToken ct = default)
    {
        try
        {
            return await (Configuration.KavitaPlusApiUrl + "/api/v3/Scrobble/user-info")
                .WithKavitaPlusHeaders(license)
                .SetQueryParam("provider", provider)
                .SetQueryParam("accessToken", token)
                .GetJsonAsync<KPlusResult<KavitaPlusUserInfo>>(cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an issue getting user info from Kavita+ for provider {Provider}", provider);
            return KPlusResult<KavitaPlusUserInfo>.Failure(ex.Message);
        }
    }

    public async Task<LicenseInfoDto?> GetLicenseInfo(CancellationToken ct = default)
    {
        try
        {
            var license = (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct)).Value;
            var response = await (Configuration.KavitaPlusApiUrl + "/api/license/info")
                .WithKavitaPlusHeaders(license)
                .GetJsonAsync<LicenseInfoDto>(cancellationToken: ct);

            return response;
        } catch (FlurlHttpException e)
        {
            logger.LogError(e, "An error happened during the request to Kavita+ API");
        }

        return null;
    }

    public async Task<LicenseInfoDto?> LinkDiscord(LinkDiscordRequestDto request, CancellationToken ct = default)
    {
        try
        {
            var license = (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct)).Value;
            var response = await (Configuration.KavitaPlusApiUrl + "/api/license/link-discord")
                .WithKavitaPlusHeaders(license)
                .PostJsonAsync(request, cancellationToken: ct)
                .ReceiveJson<LicenseInfoDto>();

            return response;
        } catch (FlurlHttpException e)
        {
            logger.LogError(e, "An error happened during the request to Kavita+ API");
        }

        return null;
    }

    /// <summary>
    /// Gets a snapshot of the Metadata providers operational health (average response time, last incident, overall status)
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<IList<KavitaPlusProviderHealthSnapshotDto>> GetProviderHealthSnapshot(CancellationToken ct = default)
    {
        try
        {
            var license = (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct)).Value;
            var response = await (Configuration.KavitaPlusApiUrl + "/api/providerhealth/snapshot")
                .WithKavitaPlusHeaders(license)
                .GetJsonAsync<IList<KavitaPlusProviderHealthSnapshotDto>>(cancellationToken: ct);

            return response;
        } catch (FlurlHttpException e)
        {
            logger.LogError(e, "An error happened during the request to Kavita+ API");
        }

        return [];
    }


    /// <summary>
    /// Gets a snapshot of the amount of usage this server has with Kavita+
    /// </summary>
    /// <param name="ct"></param>
    /// <returns>Returns an empty object on errors</returns>
    public async Task<KavitaPlusLicenseUsageDto> GetLicenseUsage(CancellationToken ct = default)
    {
        try
        {
            var license = (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct)).Value;
            var response = await (Configuration.KavitaPlusApiUrl + "/api/stats/")
                .WithKavitaPlusHeaders(license)
                .GetJsonAsync<KPlusResult<KavitaPlusLicenseUsageDto>>(cancellationToken: ct);

            if (response.IsSuccess) return response.Data!;
            logger.LogError(response.ErrorMessage, "Unable to pull license usage data from Kavita+ API");
        } catch (FlurlHttpException e)
        {
            logger.LogError(e, "An error happened during the request to Kavita+ API");
        }

        return new KavitaPlusLicenseUsageDto()
        {
            GeneratedAtUtc =  DateTime.UtcNow,
            Stats = []
        };
    }

    public async Task<bool> CancelLicenseAsync(CancelKavitaPlusLicenseDto dto, CancellationToken ct)
    {
        try
        {
            var license = (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct)).Value;
            var response = await (Configuration.KavitaPlusApiUrl + "/api/license/cancel")
                .WithKavitaPlusHeaders(license)
                .PostJsonAsync(dto, cancellationToken: ct)
                .ReceiveJson<KPlusResult<object>>();

            if (response.IsSuccess) return true;
            logger.LogError("Unable to cancel subscription on Kavita+ API: {Error}", response.ErrorMessage);
        } catch (FlurlHttpException e)
        {
            logger.LogError(e, "An error happened during the request to Kavita+ API");
        }

        return false;
    }

    public async Task<IList<KavitaPlusProductInfoDto>> GetProducts(CancellationToken ct = default)
    {
        try
        {
            var license = (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct)).Value;
            return await (Configuration.KavitaPlusApiUrl + "/api/license/products")
                .WithKavitaPlusHeaders(license)
                .GetJsonAsync<IList<KavitaPlusProductInfoDto>>(cancellationToken: ct);
        } catch (FlurlHttpException e)
        {
            logger.LogError(e, "An error happened during the request to Kavita+ API");
        }

        return [];
    }

    public async Task<string?> RenewLicenseAsync(RenewKavitaPlusLicenseDto dto, CancellationToken ct)
    {
        try
        {
            var license = (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct)).Value;
            var response = await (Configuration.KavitaPlusApiUrl + "/api/license/renew")
                .WithKavitaPlusHeaders(license)
                .PostJsonAsync(dto, cancellationToken: ct)
                .ReceiveJson<KPlusResult<RenewSubscriptionResponseDto>>();

            if (response.IsSuccess) return response.Data?.CheckoutUrl;
            logger.LogError("Unable to renew subscription on Kavita+ API: {Error}", response.ErrorMessage);
        } catch (FlurlHttpException e)
        {
            logger.LogError(e, "An error happened during the request to Kavita+ API");
        }

        return null;
    }

    public async Task<bool> ChangeEmail(ChangeEmailOnLicenseDto dto, CancellationToken ct)
    {
        try
        {
            var license = (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct)).Value;
            var response = await (Configuration.KavitaPlusApiUrl + "/api/license/change-email")
                .WithKavitaPlusHeaders(license)
                .PostJsonAsync(dto, cancellationToken: ct)
                .ReceiveJson<KPlusResult<bool>>(); // It just returns blank result

            if (response.IsSuccess) return response.IsSuccess;
            logger.LogError("Unable to change Kavita+ email: {Error}", response.ErrorMessage);
        } catch (FlurlHttpException e)
        {
            logger.LogError(e, "An error happened during the request to Kavita+ API");
        }

        return false;
    }

    public async Task<KPlusResult<string>> StartOAuthFlow(OAuthUpstream upstream, string instanceUrl, string apiKey,
        CancellationToken ct = default)
    {
        var license = (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct)).Value;

        var body = new StartOAuthFlowRequestDto
        {
            Upstream = upstream,
            InstanceUrl = instanceUrl,
            ApiKey = _dataProtector.Protect(apiKey)
        };

        try
        {
            var response = await (Configuration.KavitaPlusApiUrl + "/api/v3/oauth/start-flow")
                .WithKavitaPlusHeaders(license)
                .PostJsonAsync(body, cancellationToken: ct)
                .ReceiveJson<KPlusResult<string>>();

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an issue starting the OAuth flow");
            return KPlusResult<string>.Failure(ex.Message);
        }
    }

    public async Task<KPlusResult<DateTime>> GetTokenExpiry(OAuthUpstream upstream, string accessToken, CancellationToken ct = default)
    {
        var license = (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct)).Value;

        try
        {
            var response = await (Configuration.KavitaPlusApiUrl + "/api/v3/oauth/token-expiration")
                .WithKavitaPlusHeaders(license)
                .SetQueryParam("upstream", upstream)
                .SetQueryParam("accessToken", accessToken)
                .GetJsonAsync<KPlusResult<DateTime>>(cancellationToken: ct);

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an issue starting refreshing tokens");
            return KPlusResult<DateTime>.Failure(ex.Message);
        }
    }

    public async Task<KPlusResult<TokenResponseDto>> RefreshToken(RefreshTokenRequestDto requestDto, CancellationToken ct = default)
    {
        var license = (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct)).Value;

        try
        {
            var response = await (Configuration.KavitaPlusApiUrl + "/api/v3/oauth/refresh-tokens")
                .WithKavitaPlusHeaders(license)
                .PostJsonAsync(requestDto, cancellationToken: ct)
                .ReceiveJson<KPlusResult<TokenResponseDto>>();

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an issue starting refreshing tokens");
            return KPlusResult<TokenResponseDto>.Failure(ex.Message);
        }
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
}
