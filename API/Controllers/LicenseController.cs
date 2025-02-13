using System;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.DTOs.KavitaPlus.License;
using API.Entities.Enums;
using API.Extensions;
using API.Services;
using API.Services.Plus;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers;

#nullable enable

public class LicenseController(
    IUnitOfWork unitOfWork,
    ILogger<LicenseController> logger,
    ILicenseService licenseService,
    ILocalizationService localizationService,
    ITaskScheduler taskScheduler)
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
        if (result)
        {
            await taskScheduler.ScheduleKavitaPlusTasks();
        }

        return Ok(result);
    }

    /// <summary>
    /// Has any license registered with the instance. Does not check Kavita+ API
    /// </summary>
    /// <returns></returns>
    [Authorize("RequireAdminRole")]
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
    [Authorize("RequireAdminRole")]
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

    [Authorize("RequireAdminRole")]
    [HttpDelete]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.LicenseCache)]
    public async Task<ActionResult> RemoveLicense()
    {
        logger.LogInformation("Removing license on file for Server");
        var setting = await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey);
        setting.Value = null;
        unitOfWork.SettingsRepository.Update(setting);
        await unitOfWork.CommitAsync();
        await taskScheduler.ScheduleKavitaPlusTasks();
        return Ok();
    }


    [Authorize("RequireAdminRole")]
    [HttpPost("reset")]
    public async Task<ActionResult> ResetLicense(UpdateLicenseDto dto)
    {
        logger.LogInformation("Resetting license on file for Server");
        if (await licenseService.ResetLicense(dto.License, dto.Email))
        {
            await taskScheduler.ScheduleKavitaPlusTasks();
            return Ok();
        }

        return BadRequest(localizationService.Translate(User.GetUserId(), "unable-to-reset-k+"));
    }

    /// <summary>
    /// Updates server license
    /// </summary>
    /// <remarks>Caches the result</remarks>
    /// <returns></returns>
    [Authorize("RequireAdminRole")]
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
            return BadRequest(await localizationService.Translate(User.GetUserId(), ex.Message));
        }
        return Ok();
    }
}
