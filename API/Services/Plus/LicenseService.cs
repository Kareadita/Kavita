using System;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.DTOs.Account;
using API.DTOs.License;
using API.Entities;
using EasyCaching.Core;
using Flurl.Http;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace API.Services.Plus;

public interface ILicenseService
{
    Task<bool> HasActiveLicense(int userId);

    Task<string> EncryptLicense(string license);
    Task ValidateAllLicenses();
    Task RemoveLicenseFromUser(AppUser user);
    Task AddLicenseToUser(AppUser user, string license);
}

public class LicenseService : ILicenseService
{
    private readonly IEasyCachingProviderFactory _cachingProviderFactory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LicenseService> _logger;
    private readonly TimeSpan _licenseCacheTimeout = TimeSpan.FromHours(8);
    public const string Cron = "0 */4 * * *";


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
        var provider = _cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.License);
        var cacheValue = await provider.GetAsync<bool>($"{userId}");
        if (cacheValue.HasValue) return cacheValue.Value;

        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
        if (user == null) return false;
        var result = await IsLicenseValid(user.License);
        await provider.SetAsync($"{userId}", result, _licenseCacheTimeout);
        // TODO: Think about an EventHub message to user when something like license changes
        return result;

    }

    /// <summary>
    /// Performs license lookup to API layer
    /// </summary>
    /// <param name="license"></param>
    /// <returns></returns>
    private async Task<bool> IsLicenseValid(string license)
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
                    License = license,
                    InstallId = serverSetting.InstallId
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
                .PostJsonAsync(new EncryptLicenseDto()
                {
                    License = license,
                    InstallId = serverSetting.InstallId
                })
                .ReceiveString();

            return response;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error happened during the request to KavitaPlus API");
            return string.Empty;
        }
    }

    /// <summary>
    /// Checks all licenses and updates cache
    /// </summary>
    /// <remarks>Expected to be called at startup and on reoccuring basis</remarks>
    public async Task ValidateAllLicenses()
    {
        _logger.LogInformation("Validating user's KavitaPlus Licenses");
        var provider = _cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.License);
        await provider.FlushAsync();

        var users = await _unitOfWork.UserRepository.GetAllUsersAsync();
        foreach (var user in users)
        {
            var isValid = await IsLicenseValid(user.License);
            if (isValid)
            {
                await provider.SetAsync($"{user.Id}", true, _licenseCacheTimeout);
            }
        }

        _logger.LogInformation("Validating user's KavitaPlus Licenses - Complete");
    }

    public async Task RemoveLicenseFromUser(AppUser user)
    {
        try
        {
            user.License = string.Empty;
            _unitOfWork.UserRepository.Update(user);
            await _unitOfWork.CommitAsync();
            var provider = _cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.License);
            await provider.RemoveAsync($"{user.Id}");
        }
        catch (Exception ex)
        {
            throw new KavitaException("Could not remove user's License", ex);
        }
    }

    public async Task AddLicenseToUser(AppUser user, string license)
    {
        try
        {
            user.License =  await EncryptLicense(license);
            _unitOfWork.UserRepository.Update(user);
            await _unitOfWork.CommitAsync();
        }
        catch (Exception ex)
        {
            throw new KavitaException("Could not remove user's License", ex);
        }
    }
}
