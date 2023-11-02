using System;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.DTOs.Account;
using API.DTOs.License;
using API.Entities.Enums;
using EasyCaching.Core;
using Flurl.Http;
using Hangfire;
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
    Task ValidateLicenseStatus();
    Task RemoveLicense();
    Task AddLicense(string license, string email);
    Task<bool> HasActiveLicense(bool forceCheck = false);
}

public class LicenseService : ILicenseService
{
    private readonly IEasyCachingProviderFactory _cachingProviderFactory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LicenseService> _logger;
    private readonly TimeSpan _licenseCacheTimeout = TimeSpan.FromHours(8);
    public const string Cron = "0 */4 * * *";
    private const string CacheKey = "license";


    public LicenseService(IEasyCachingProviderFactory cachingProviderFactory, IUnitOfWork unitOfWork, ILogger<LicenseService> logger)
    {
        _cachingProviderFactory = cachingProviderFactory;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }


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
            _logger.LogError(e, "An error happened during the request to Kavita+ API");
            throw;
        }
    }

    /// <summary>
    /// Register the license with KavitaPlus
    /// </summary>
    /// <param name="license"></param>
    /// <param name="email"></param>
    /// <returns></returns>
    private async Task<string> RegisterLicense(string license, string email)
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
                    EmailId = email.Trim()
                })
                .ReceiveJson<RegisterLicenseResponseDto>();

            if (response.Successful)
            {
                return response.EncryptedLicense;
            }

            _logger.LogError("An error happened during the request to Kavita+ API: {ErrorMessage}", response.ErrorMessage);
            throw new KavitaException(response.ErrorMessage);
        }
        catch (FlurlHttpException e)
        {
            _logger.LogError(e, "An error happened during the request to Kavita+ API");
            return string.Empty;
        }
    }

    /// <summary>
    /// Checks licenses and updates cache
    /// </summary>
    /// <remarks>Expected to be called at startup and on reoccurring basis</remarks>
    public async Task ValidateLicenseStatus()
    {
        var provider = _cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.License);
        try
        {
            var license = await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey);
            if (string.IsNullOrEmpty(license.Value)) {
                await provider.SetAsync(CacheKey, false, _licenseCacheTimeout);
                return;
            }

            _logger.LogInformation("Validating Kavita+ License");

            await provider.FlushAsync();
            var isValid = await IsLicenseValid(license.Value);
            await provider.SetAsync(CacheKey, isValid, _licenseCacheTimeout);

            _logger.LogInformation("Validating Kavita+ License - Complete");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an error talking with Kavita+ API for license validation. Rescheduling check in 30 mins");
            await provider.SetAsync(CacheKey, false, _licenseCacheTimeout);
            BackgroundJob.Schedule(() => ValidateLicenseStatus(), TimeSpan.FromMinutes(30));
        }
    }

    public async Task RemoveLicense()
    {
        var serverSetting = await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey);
        serverSetting.Value = string.Empty;
        _unitOfWork.SettingsRepository.Update(serverSetting);
        await _unitOfWork.CommitAsync();
        var provider = _cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.License);
        await provider.RemoveAsync(CacheKey);
    }

    public async Task AddLicense(string license, string email)
    {
        var serverSetting = await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey);
        var lic = await RegisterLicense(license, email);
        if (string.IsNullOrWhiteSpace(lic))
            throw new KavitaException("unable-to-register-k+");
        serverSetting.Value = lic;
        _unitOfWork.SettingsRepository.Update(serverSetting);
        await _unitOfWork.CommitAsync();
    }

    public async Task<bool> HasActiveLicense(bool forceCheck = false)
    {
        var provider = _cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.License);
        if (!forceCheck)
        {
            var cacheValue = await provider.GetAsync<bool>(CacheKey);
            if (cacheValue.HasValue) return cacheValue.Value;
        }

        try
        {
            var serverSetting = await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey);
            var result = await IsLicenseValid(serverSetting.Value);
            await provider.FlushAsync();
            await provider.SetAsync(CacheKey, result, _licenseCacheTimeout);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an issue connecting to Kavita+");
        }

        return false;
    }
}
