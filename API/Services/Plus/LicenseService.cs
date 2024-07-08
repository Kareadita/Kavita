using System;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.DTOs.Account;
using API.DTOs.License;
using API.Entities.Enums;
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
}

public class LicenseService(
    IEasyCachingProviderFactory cachingProviderFactory,
    IUnitOfWork unitOfWork,
    ILogger<LicenseService> logger)
    : ILicenseService
{
    private readonly TimeSpan _licenseCacheTimeout = TimeSpan.FromHours(8);
    public const string Cron = "0 */4 * * *";
    private const string CacheKey = "license";


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
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .WithHeader("x-license-key", license)
                .WithHeader("x-installId", HashUtil.ServerToken())
                .WithHeader("x-kavita-version", BuildInfo.Version)
                .WithHeader("Content-Type", "application/json")
                .WithTimeout(TimeSpan.FromSeconds(Configuration.DefaultTimeOutSecs))
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
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .WithHeader("x-license-key", license)
                .WithHeader("x-installId", HashUtil.ServerToken())
                .WithHeader("x-kavita-version", BuildInfo.Version)
                .WithHeader("Content-Type", "application/json")
                .WithTimeout(TimeSpan.FromSeconds(Configuration.DefaultTimeOutSecs))
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
    /// <remarks>Expected to be called at startup and on reoccurring basis</remarks>
    // public async Task ValidateLicenseStatus()
    // {
    //     var provider = _cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.License);
    //     try
    //     {
    //         var license = await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey);
    //         if (string.IsNullOrEmpty(license.Value)) {
    //             await provider.SetAsync(CacheKey, false, _licenseCacheTimeout);
    //             return;
    //         }
    //
    //         _logger.LogInformation("Validating Kavita+ License");
    //
    //         await provider.FlushAsync();
    //         var isValid = await IsLicenseValid(license.Value);
    //         await provider.SetAsync(CacheKey, isValid, _licenseCacheTimeout);
    //
    //         _logger.LogInformation("Validating Kavita+ License - Complete");
    //     }
    //     catch (Exception ex)
    //     {
    //         _logger.LogError(ex, "There was an error talking with Kavita+ API for license validation. Rescheduling check in 30 mins");
    //         await provider.SetAsync(CacheKey, false, _licenseCacheTimeout);
    //         BackgroundJob.Schedule(() => ValidateLicenseStatus(), TimeSpan.FromMinutes(30));
    //     }
    // }

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
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .WithHeader("x-license-key", license)
                .WithHeader("x-installId", HashUtil.ServerToken())
                .WithHeader("x-kavita-version", BuildInfo.Version)
                .WithHeader("Content-Type", "application/json")
                .WithTimeout(TimeSpan.FromSeconds(Configuration.DefaultTimeOutSecs))
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
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .WithHeader("x-license-key", encryptedLicense.Value)
                .WithHeader("x-installId", HashUtil.ServerToken())
                .WithHeader("x-kavita-version", BuildInfo.Version)
                .WithHeader("Content-Type", "application/json")
                .WithTimeout(TimeSpan.FromSeconds(Configuration.DefaultTimeOutSecs))
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
}
