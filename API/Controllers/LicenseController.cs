using System;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.DTOs.KavitaPlus.License;
using API.Entities.Enums;
using API.Services;
using API.Services.Plus;
using EasyCaching.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TaskScheduler = API.Services.TaskScheduler;

namespace API.Controllers;

#nullable enable

public class LicenseController(
    IUnitOfWork unitOfWork,
    ILogger<LicenseController> logger,
    ILicenseService licenseService,
    ILocalizationService localizationService,
    ITaskScheduler taskScheduler,
    IEasyCachingProviderFactory cachingProviderFactory)
    : BaseApiController
{
    /// <summary>
    /// Checks if the user's license is valid or not
    /// </summary>
    /// <returns></returns>
    [HttpGet("valid-license")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.LicenseCache)]
    public async Task<ActionResult<bool>> HasValidLicense(bool forceCheck = false)
    {

        var result = await licenseService.HasActiveLicense(forceCheck);

        var licenseInfoProvider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.License);
        var cacheValue = await licenseInfoProvider.GetAsync<bool>(LicenseService.CacheKey);

        if (result && !cacheValue.IsNull && !cacheValue.Value)
        {
            await taskScheduler.ScheduleKavitaPlusTasks();
        }

        return Ok(result);
    }

    /// <summary>
    /// Has any license registered with the instance. Does not validate against Kavita+ API
    /// </summary>
    /// <returns></returns>
    [Authorize(PolicyGroups.AdminPolicy)]
    [HttpGet("has-license")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.LicenseCache)]
    public async Task<ActionResult<bool>> HasLicense()
    {
        return Ok(!string.IsNullOrEmpty(
            (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey)).Value));
    }

    /// <summary>
    /// Asks Kavita+ for the latest license info
    /// </summary>
    /// <param name="forceCheck">Force checking the API and skip the 8 hour cache</param>
    /// <returns></returns>
    [Authorize(PolicyGroups.AdminPolicy)]
    [HttpGet("info")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.LicenseCache)]
    public async Task<ActionResult<LicenseInfoDto?>> GetLicenseInfo(bool forceCheck = false)
    {
        try
        {
            return Ok(await licenseService.GetLicenseInfo(forceCheck));
        }
        catch (Exception)
        {
            return Ok(null);
        }
    }

    /// <summary>
    /// Remove the Kavita+ License on the Server
    /// </summary>
    /// <returns></returns>
    [Authorize(PolicyGroups.AdminPolicy)]
    [HttpDelete]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.LicenseCache)]
    public async Task<ActionResult> RemoveLicense()
    {
        logger.LogInformation("Removing license on file for Server");
        var setting = await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey);
        setting.Value = null;
        unitOfWork.SettingsRepository.Update(setting);
        await unitOfWork.CommitAsync();

        TaskScheduler.RemoveKavitaPlusTasks();

        return Ok();
    }


    [Authorize(PolicyGroups.AdminPolicy)]
    [HttpPost("reset")]
    public async Task<ActionResult> ResetLicense(UpdateLicenseDto dto)
    {
        logger.LogInformation("Resetting license on file for Server");
        if (await licenseService.ResetLicense(dto.License, dto.Email))
        {
            await taskScheduler.ScheduleKavitaPlusTasks();
            return Ok();
        }

        return BadRequest(localizationService.Translate(UserId, "unable-to-reset-k+"));
    }

    /// <summary>
    /// Resend the welcome email to the user
    /// </summary>
    /// <returns></returns>
    [HttpPost("resend-license")]
    public async Task<ActionResult<bool>> ResendWelcomeEmail()
    {
       return Ok(await licenseService.ResendWelcomeEmail());
    }

    /// <summary>
    /// Updates server license
    /// </summary>
    /// <remarks>Caches the result</remarks>
    /// <returns></returns>
    [Authorize(PolicyGroups.AdminPolicy)]
    [HttpPost]
    public async Task<ActionResult> UpdateLicense(UpdateLicenseDto dto)
    {
        try
        {
            await licenseService.AddLicense(dto.License.Trim(), dto.Email.Trim(), dto.DiscordId);
            await taskScheduler.ScheduleKavitaPlusTasks();
        }
        catch (Exception ex)
        {
            return BadRequest(await localizationService.Translate(UserId, ex.Message));
        }
        return Ok();
    }
}
