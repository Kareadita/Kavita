using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyCaching.Core;
using Flurl.Http;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.Plus;
using Kavita.API.Services.SignalR;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Kavita.Common.Extensions;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.KavitaPlus.License;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities.Enums;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Plus;

internal class RegisterLicenseResponseDto
{
    public string EncryptedLicense { get; set; }
    public bool Successful { get; set; }
    public string ErrorMessage { get; set; }
    public bool IsSubscriptionActive { get; set; }
    public KavitaPlusRegistrationErrorCode ErrorCode { get; set; }
}

public class LicenseService(
    IEasyCachingProviderFactory cachingProviderFactory,
    IUnitOfWork unitOfWork,
    ILogger<LicenseService> logger,
    IVersionUpdaterService versionUpdaterService, IKavitaPlusApiService kavitaPlusApiService,
    IFileCacheService fileCacheService,
    IEventHub eventHub)
    : ILicenseService
{
    private readonly TimeSpan _licenseCacheTimeout = TimeSpan.FromHours(8);
    public const string Cron = "0 */9 * * *";
    /// <summary>
    /// Cache key for if license is valid or not
    /// </summary>
    public const string CacheKey = "license";
    private const string LicenseInfoCacheKey = "license-info";
    private const string LicenseUsageCacheKey = "license-usage";


    /// <summary>
    /// Performs license lookup to API layer
    /// </summary>
    /// <param name="license"></param>
    /// <returns></returns>
    private async Task<bool> IsLicenseValid(string license)
    {
        if (string.IsNullOrWhiteSpace(license)) return false;
        try
        {
            var response = await (Configuration.KavitaPlusApiUrl + "/api/license/check")
                .WithKavitaPlusHeaders(license)
                .PostJsonAsync(new LicenseValidDto()
                {
                    License = license,
                    InstallId = HashUtil.ServerToken()
                })
                .ReceiveString();
            return bool.Parse(response);
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error happened during the request to Kavita+ API");
            throw;
        }
    }

    private async Task<RegisterLicenseResponseDto> RegisterLicense(string license, string email, string? discordId)
    {
        if (string.IsNullOrWhiteSpace(license) || string.IsNullOrWhiteSpace(email))
        {
            return new RegisterLicenseResponseDto()
            {
                EncryptedLicense = string.Empty,
                ErrorCode = KavitaPlusRegistrationErrorCode.RegistrationFailed,
                Successful = false
            };
        }

        try
        {
            var response = await (Configuration.KavitaPlusApiUrl + "/api/license/register")
                .WithKavitaPlusHeaders(license)
                .PostJsonAsync(new EncryptLicenseDto()
                {
                    License = license.Trim(),
                    InstallId = HashUtil.ServerToken(),
                    EmailId = email.Trim(),
                    DiscordId = discordId?.Trim()
                })
                .ReceiveJson<RegisterLicenseResponseDto>();

            if (response.Successful)
            {
                await eventHub.SendMessageAsync(MessageFactory.LicenseInfoUpdate, MessageFactory.LicenseInfoUpdateEvent());

                return response;
            }

            logger.LogError("Kavita+ registration failed. Code: {Code}, Message: {Message}", response.ErrorCode, response.ErrorMessage);
            return response;
        }
        catch (FlurlHttpException e)
        {
            logger.LogError(e, "Network error reaching Kavita+ API");
            return new RegisterLicenseResponseDto()
            {
                EncryptedLicense = string.Empty,
                ErrorCode = KavitaPlusRegistrationErrorCode.InternalError,
                Successful = false
            };
        }
    }


    /// <summary>
    /// Checks licenses and updates cache
    /// </summary>
    /// <param name="forceCheck">Skip what's in cache</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<bool> HasActiveLicense(bool forceCheck = false, CancellationToken ct = default)
    {
        var provider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.License);
        if (!forceCheck)
        {
            var cacheValue = await provider.GetAsync<bool>(CacheKey, ct);
            if (cacheValue.HasValue) return cacheValue.Value;
        }

        var result = false;
        try
        {
            var serverSetting = await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct);
            result = await IsLicenseValid(serverSetting.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an issue connecting to Kavita+");
        }
        finally
        {
            await provider.FlushAsync(ct);
            await provider.SetAsync(CacheKey, result, _licenseCacheTimeout, ct);
        }

        return result;
    }

    /// <summary>
    /// Checks if the sub is active and caches the result. This should not be used too much over cache as it will skip backend caching.
    /// </summary>
    /// <param name="license"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<bool> HasActiveSubscription(string? license, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(license)) return false;
        try
        {
            var response = await (Configuration.KavitaPlusApiUrl + "/api/license/check-sub")
                .WithKavitaPlusHeaders(license)
                .PostJsonAsync(new LicenseValidDto()
                {
                    License = license,
                    InstallId = HashUtil.ServerToken()
                }, cancellationToken: ct)
                .ReceiveString();

            var result =  bool.Parse(response);

            var provider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.License);
            await provider.FlushAsync(ct);
            await provider.SetAsync(CacheKey, result, _licenseCacheTimeout, ct);

            return result;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error happened during the request to Kavita+ API");
            return false;
        }
    }

    public async Task RemoveLicense(CancellationToken ct = default)
    {
        var serverSetting = await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct);
        serverSetting.Value = string.Empty;
        unitOfWork.SettingsRepository.Update(serverSetting);
        await unitOfWork.CommitAsync(ct);

        var provider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.License);
        await provider.RemoveAsync(CacheKey, ct);

        await eventHub.SendMessageAsync(MessageFactory.LicenseInfoUpdate, MessageFactory.LicenseInfoUpdateEvent(), ct: ct);
    }

    public async Task<KavitaPlusRegisterResultDto> AddLicense(string license, string email, string? discordId, CancellationToken ct = default)
    {
        var response = await RegisterLicense(license, email, discordId);
        if (string.IsNullOrWhiteSpace(response.EncryptedLicense) || !response.Successful)
            return new KavitaPlusRegisterResultDto { Success = false, ErrorCode = response.ErrorCode };

        var serverSetting = await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct);
        serverSetting.Value = response.EncryptedLicense;
        unitOfWork.SettingsRepository.Update(serverSetting);
        await unitOfWork.CommitAsync(ct);

        if (response is {Successful: true})
        {
            var provider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.License);
            await provider.FlushAsync(ct);
            await provider.SetAsync(CacheKey, response.IsSubscriptionActive, _licenseCacheTimeout, ct);

            await eventHub.SendMessageAsync(MessageFactory.LicenseInfoUpdate, MessageFactory.LicenseInfoUpdateEvent(), ct: ct);
        }

        return new KavitaPlusRegisterResultDto { Success = true, IsSubscriptionActive = response.IsSubscriptionActive};
    }


    /// <summary>
    /// Removes this installId from Kavita+, essentially allowing the user to re-register an instance
    /// </summary>
    /// <param name="license"></param>
    /// <param name="email"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="KavitaException"></exception>
    public async Task<bool> ResetLicense(string license, string email, CancellationToken ct = default)
    {
        try
        {
            var encryptedLicense = await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct);
            var response = await (Configuration.KavitaPlusApiUrl + "/api/license/reset")
                .WithKavitaPlusHeaders(encryptedLicense.Value)
                .PostJsonAsync(new ResetLicenseDto()
                {
                    License = license.Trim(),
                    InstallId = HashUtil.ServerToken(),
                    EmailId = email
                }, cancellationToken: ct)
                .ReceiveString();

            if (string.IsNullOrEmpty(response))
            {
                var provider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.License);
                await provider.RemoveAsync(CacheKey, ct);

                await eventHub.SendMessageAsync(MessageFactory.LicenseInfoUpdate, MessageFactory.LicenseInfoUpdateEvent(), ct: ct);

                return true;
            }

            logger.LogError("An error happened during the request to Kavita+ API: {ErrorMessage}", response);
            throw new KavitaException(response);
        }
        catch (FlurlHttpException e)
        {
            logger.LogError(e, "An error happened during the request to Kavita+ API");
        }

        return false;
    }

    /// <summary>
    /// Fetches information about the license from Kavita+. If there is no license or an exception, will return null and can be assumed it is not active
    /// </summary>
    /// <param name="forceCheck"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<LicenseInfoDto?> GetLicenseInfo(bool forceCheck = false, CancellationToken ct = default)
    {
        // Check if there is a license
        var hasLicense =
            !string.IsNullOrEmpty((await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct))
                .Value);

        if (!hasLicense) return null;

        // Check the cache
        var licenseInfoProvider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.LicenseInfo);
        if (!forceCheck)
        {
            var cacheValue = await licenseInfoProvider.GetAsync<LicenseInfoDto>(LicenseInfoCacheKey, ct);
            if (cacheValue.HasValue) return cacheValue.Value;
        }



        try
        {
            var response = await kavitaPlusApiService.GetLicenseInfo(ct);

            // This indicates a mismatch on installId or no active subscription
            if (response == null) return null;

            await EnrichLicenseInfo(response, hasLicense, ct);

            // Cache if the license is valid here as well
            var licenseProvider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.License);
            await licenseProvider.SetAsync(CacheKey, response.IsActive, _licenseCacheTimeout, ct);

            // Always cache the response as we provide this on expired licenses
            await licenseInfoProvider.SetAsync(LicenseInfoCacheKey, response, _licenseCacheTimeout, ct);

            await eventHub.SendMessageAsync(MessageFactory.LicenseInfoUpdate, MessageFactory.LicenseInfoUpdateEvent(), ct: ct);

            return response;
        }
        catch (FlurlHttpException e)
        {
            logger.LogError(e, "An error happened during the request to Kavita+ API");
        }

        return null;
    }

    /// <summary>
    /// Attempts to resend a welcome email to the registered user. The sub does not need to be active.
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<bool> ResendWelcomeEmail(CancellationToken ct = default)
    {
        try
        {
            var encryptedLicense = await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct);
            if (string.IsNullOrEmpty(encryptedLicense.Value)) return false;

            var httpResponse = await (Configuration.KavitaPlusApiUrl + "/api/license/resend-welcome-email")
                .WithKavitaPlusHeaders(encryptedLicense.Value)
                .PostAsync(cancellationToken: ct);

            var response = await httpResponse.GetStringAsync();

            if (response == null) return false;


            return response == "true";
        }
        catch (FlurlHttpException e)
        {
            logger.LogError(e, "An error happened during the request to Kavita+ API");
        }

        return false;
    }

    public async Task<KavitaPlusLicenseUsageDto> GetLicenseUsage(CancellationToken ct = default)
    {
        // Expired licenses won't generate new usage, so cache them long term (1 month)
        // active licenses refresh every 4 hours.
        var ttl = await HasActiveLicense(ct: ct)
            ? TimeSpan.FromHours(4)
            : TimeSpan.FromDays(30);

        var result = await fileCacheService.GetOrFetchAsync<KavitaPlusLicenseUsageDto>(
            LicenseUsageCacheKey,
            FileCacheService.KavitaPlusCacheDirectory,
            ttl,
            async _ => await kavitaPlusApiService.GetLicenseUsage(ct),
            shouldCache: r => r?.Stats?.Count > 0,
            ct: ct);

        return result ?? new KavitaPlusLicenseUsageDto
        {
            GeneratedAtUtc = DateTime.UtcNow,
            Stats = []
        };
    }

    public async Task<bool> CancelLicense(CancelKavitaPlusLicenseDto dto, CancellationToken ct)
    {
        return await kavitaPlusApiService.CancelLicenseAsync(dto, ct);
    }

    public async Task<IList<KavitaPlusProductInfoDto>> GetProducts(CancellationToken ct = default)
    {
        return await kavitaPlusApiService.GetProducts(ct);
    }

    public async Task<string?> RenewLicense(RenewKavitaPlusLicenseDto dto, CancellationToken ct)
    {
        return await kavitaPlusApiService.RenewLicenseAsync(dto, ct);
    }

    public async Task<bool> ChangeEmail(ChangeEmailOnLicenseDto dto, CancellationToken ct)
    {
        return await kavitaPlusApiService.ChangeEmail(dto, ct);
    }

    public async Task LinkDiscord(string discordId, string discordUsername, CancellationToken ct = default)
    {
        var hasLicense = !string.IsNullOrEmpty((await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct))
                .Value);

        if (!hasLicense) return;

        try
        {
            var response = await kavitaPlusApiService.LinkDiscord(new LinkDiscordRequestDto
            {
                DiscordId = discordId,
                DiscordUserName = discordUsername
            }, ct);

            if (response == null)
            {
                logger.LogError("Failed to link discord info");
                return;
            }

            await EnrichLicenseInfo(response, hasLicense, ct);

            var licenseInfoProvider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.LicenseInfo);
            var licenseProvider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.License);

            await licenseProvider.SetAsync(CacheKey, response.IsActive, _licenseCacheTimeout, ct);
            await licenseInfoProvider.SetAsync(LicenseInfoCacheKey, response, _licenseCacheTimeout, ct);

            await eventHub.SendMessageAsync(MessageFactory.LicenseInfoUpdate, MessageFactory.LicenseInfoUpdateEvent(), ct: ct);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to link discord info");
        }
    }

    private async Task EnrichLicenseInfo(LicenseInfoDto licenseInfo, bool hasLicense, CancellationToken ct)
    {
        // Ensure that current version is within the 3 version limit. Don't count Nightly releases or Hotfixes
        var releases = await versionUpdaterService.GetAllReleases(ct: ct);
        licenseInfo.IsValidVersion = releases
            .Where(r => !r.UpdateTitle.Contains("Hotfix")) // We don't care about Hotfix releases
            .Where(r => !r.IsPrerelease) // Ensure we don't take current nightlies within the current/last stable
            .Take(3)
            .All(r => new Version(r.UpdateVersion) <= BuildInfo.Version);

        licenseInfo.HasLicense = hasLicense;
        licenseInfo.InstallId = HashUtil.ServerToken();
    }
}
