using System;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.DTOs.Account;
using API.DTOs.KavitaPlus.License;
using API.Entities.Enums;
using API.Extensions;
using API.Services.Tasks;
using EasyCaching.Core;
using Flurl.Http;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Microsoft.Extensions.Logging;

namespace API.Services.Plus;
#nullable enable

internal class RegisterLicenseResponseDto
{
    public string EncryptedLicense { get; set; }
    public bool Successful { get; set; }
    public string ErrorMessage { get; set; }
}

public interface ILicenseService
{
    //Task ValidateLicenseStatus();
    Task RemoveLicense();
    Task AddLicense(string license, string email, string? discordId);
    Task<bool> HasActiveLicense(bool forceCheck = false);
    Task<bool> HasActiveSubscription(string? license);
    Task<bool> ResetLicense(string license, string email);
    Task<LicenseInfoDto?> GetLicenseInfo(bool forceCheck = false);
}

public class LicenseService(
    IEasyCachingProviderFactory cachingProviderFactory,
    IUnitOfWork unitOfWork,
    ILogger<LicenseService> logger,
    IVersionUpdaterService versionUpdaterService)
    : ILicenseService
{
    private readonly TimeSpan _licenseCacheTimeout = TimeSpan.FromHours(8);
    public const string Cron = "0 */9 * * *";
    private const string CacheKey = "license";
    private const string LicenseInfoCacheKey = "license-info";


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

    /// <summary>
    /// Register the license with KavitaPlus
    /// </summary>
    /// <param name="license"></param>
    /// <param name="email"></param>
    /// <returns></returns>
    private async Task<string> RegisterLicense(string license, string email, string? discordId)
    {
        if (string.IsNullOrWhiteSpace(license) || string.IsNullOrWhiteSpace(email)) return string.Empty;
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
                return response.EncryptedLicense;
            }

            logger.LogError("An error happened during the request to Kavita+ API: {ErrorMessage}", response.ErrorMessage);
            throw new KavitaException(response.ErrorMessage);
        }
        catch (FlurlHttpException e)
        {
            logger.LogError(e, "An error happened during the request to Kavita+ API");
            return string.Empty;
        }
    }


    /// <summary>
    /// Checks licenses and updates cache
    /// </summary>
    /// <param name="forceCheck">Skip what's in cache</param>
    /// <returns></returns>
    public async Task<bool> HasActiveLicense(bool forceCheck = false)
    {
        var provider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.License);
        if (!forceCheck)
        {
            var cacheValue = await provider.GetAsync<bool>(CacheKey);
            if (cacheValue.HasValue) return cacheValue.Value;
        }

        try
        {
            var serverSetting = await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey);
            var result = await IsLicenseValid(serverSetting.Value);
            await provider.FlushAsync();
            await provider.SetAsync(CacheKey, result, _licenseCacheTimeout);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an issue connecting to Kavita+");
            await provider.FlushAsync();
            await provider.SetAsync(CacheKey, false, _licenseCacheTimeout);
        }

        return false;
    }

    /// <summary>
    /// Checks if the sub is active and caches the result. This should not be used too much over cache as it will skip backend caching.
    /// </summary>
    /// <param name="license"></param>
    /// <returns></returns>
    public async Task<bool> HasActiveSubscription(string? license)
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
                })
                .ReceiveString();

            var result =  bool.Parse(response);

            var provider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.License);
            await provider.FlushAsync();
            await provider.SetAsync(CacheKey, result, _licenseCacheTimeout);

            return result;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error happened during the request to Kavita+ API");
            return false;
        }
    }

    public async Task RemoveLicense()
    {
        var serverSetting = await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey);
        serverSetting.Value = string.Empty;
        unitOfWork.SettingsRepository.Update(serverSetting);
        await unitOfWork.CommitAsync();

        var provider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.License);
        await provider.RemoveAsync(CacheKey);


    }

    public async Task AddLicense(string license, string email, string? discordId)
    {
        var serverSetting = await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey);
        var lic = await RegisterLicense(license, email, discordId);
        if (string.IsNullOrWhiteSpace(lic))
            throw new KavitaException("unable-to-register-k+");
        serverSetting.Value = lic;
        unitOfWork.SettingsRepository.Update(serverSetting);
        await unitOfWork.CommitAsync();
    }



    public async Task<bool> ResetLicense(string license, string email)
    {
        try
        {
            var encryptedLicense = await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey);
            var response = await (Configuration.KavitaPlusApiUrl + "/api/license/reset")
                .WithKavitaPlusHeaders(encryptedLicense.Value)
                .PostJsonAsync(new ResetLicenseDto()
                {
                    License = license.Trim(),
                    InstallId = HashUtil.ServerToken(),
                    EmailId = email
                })
                .ReceiveString();

            if (string.IsNullOrEmpty(response))
            {
                var provider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.License);
                await provider.RemoveAsync(CacheKey);
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
    /// <returns></returns>
    public async Task<LicenseInfoDto?> GetLicenseInfo(bool forceCheck = false)
    {
        // Check if there is a license
        var hasLicense =
            !string.IsNullOrEmpty((await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey))
                .Value);

        if (!hasLicense) return null;

        // Check the cache
        var licenseInfoProvider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.LicenseInfo);
        if (!forceCheck)
        {
            var cacheValue = await licenseInfoProvider.GetAsync<LicenseInfoDto>(LicenseInfoCacheKey);
            if (cacheValue.HasValue) return cacheValue.Value;
        }


        try
        {
            var encryptedLicense = await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey);
            var response = await (Configuration.KavitaPlusApiUrl + "/api/license/info")
                .WithKavitaPlusHeaders(encryptedLicense.Value)
                .GetJsonAsync<LicenseInfoDto>();

            // This indicates a mismatch on installId or no active subscription
            if (response == null) return null;

            // Ensure that current version is within the 3 version limit. Don't count Nightly releases or Hotfixes
            var releases = await versionUpdaterService.GetAllReleases();
            response.IsValidVersion = releases
                .Where(r => !r.UpdateTitle.Contains("Hotfix")) // We don't care about Hotfix releases
                .Where(r => !r.IsPrerelease || BuildInfo.Version.IsWithinStableRelease(new Version(r.UpdateVersion))) // Ensure we don't take current nightlies within the current/last stable
                .Take(3)
                .All(r => new Version(r.UpdateVersion) <= BuildInfo.Version);

            response.HasLicense = hasLicense;

            // Cache if the license is valid here as well
            var licenseProvider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.License);
            await licenseProvider.SetAsync(CacheKey, response.IsActive, _licenseCacheTimeout);

            // Cache the license info if IsActive and ExpirationDate > DateTime.UtcNow + 2
            if (response.IsActive && response.ExpirationDate > DateTime.UtcNow.AddDays(2))
            {
                await licenseInfoProvider.SetAsync(LicenseInfoCacheKey, response, _licenseCacheTimeout);
            }

            return response;
        }
        catch (FlurlHttpException e)
        {
            logger.LogError(e, "An error happened during the request to Kavita+ API");
        }

        return null;
    }
}
