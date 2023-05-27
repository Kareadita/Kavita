using System;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Account;
using EasyCaching.Core;
using Flurl.Http;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace API.Services.Plus;

public interface ILicenseService
{
    Task<bool> IsLicenseValid(string license);

    Task<string> EncryptLicense(string license);
}

public class LicenseService : ILicenseService
{
    private readonly IEasyCachingProviderFactory _cachingProviderFactory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LicenseService> _logger;


    public LicenseService(IEasyCachingProviderFactory cachingProviderFactory, IUnitOfWork unitOfWork, ILogger<LicenseService> logger)
    {
        _cachingProviderFactory = cachingProviderFactory;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Checks if the user has an active/valid license
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<bool> HasActiveLicense(int userId)
    {
        var provider = _cachingProviderFactory.GetCachingProvider("licenseValid");
        var cacheValue = await provider.GetAsync<bool>($"{userId}");
        if (cacheValue.HasValue) return cacheValue.Value;
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
        if (user == null) return false;
        var result = await IsLicenseValid(user.License);
        await provider.SetAsync($"{userId}", result, TimeSpan.FromHours(8));
        return result;

    }

    public async Task<bool> IsLicenseValid(string license)
    {
        if (string.IsNullOrEmpty(license)) return false;
        var serverSetting = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        try
        {
            var response = await (Configuration.KavitaPlusApiUrl + "/api/license/valid")
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .WithHeader("x-license-key", serverSetting.LicenseKey)
                .WithHeader("x-installId", serverSetting.InstallId)
                .WithHeader("x-kavita-version", BuildInfo.Version)
                .WithHeader("Content-Type", "application/json")
                .WithTimeout(TimeSpan.FromSeconds(Configuration.DefaultTimeOutSecs))
                .PostJsonAsync(new UpdateLicenseDto()
                {
                    License = license
                });

            if (response.StatusCode != StatusCodes.Status200OK)
            {
                _logger.LogError("KavitaPlus API did not respond successfully. {Content}", response);
                return false;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error happened during the request to KavitaPlus API");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Sends to KavitaPlus API to encrypt the key
    /// </summary>
    /// <param name="license"></param>
    /// <returns></returns>
    public async Task<string> EncryptLicense(string license)
    {
        if (string.IsNullOrEmpty(license)) return string.Empty;
        var serverSetting = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        try
        {
            var response = await (Configuration.KavitaPlusApiUrl + "/api/license/encrypt")
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .WithHeader("x-license-key", serverSetting.LicenseKey)
                .WithHeader("x-installId", serverSetting.InstallId)
                .WithHeader("x-kavita-version", BuildInfo.Version)
                .WithHeader("Content-Type", "application/json")
                .WithTimeout(TimeSpan.FromSeconds(Configuration.DefaultTimeOutSecs))
                .PostJsonAsync(new UpdateLicenseDto()
                {
                    License = license
                });

            if (response.StatusCode != StatusCodes.Status200OK)
            {
                _logger.LogError("KavitaPlus API did not respond successfully. {Content}", response);
                return string.Empty;
            }

            return response.ResponseMessage.ReasonPhrase;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error happened during the request to KavitaPlus API");
            return string.Empty;
        }
        return string.Empty;
    }
}
