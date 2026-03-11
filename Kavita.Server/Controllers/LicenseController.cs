using System;
using System.Threading.Tasks;
using EasyCaching.Core;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.Plus;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.KavitaPlus.License;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Plus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TaskScheduler = Kavita.Services.TaskScheduler;

namespace Kavita.Server.Controllers;

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
    [HttpGet("has-license")]
    [Authorize(PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<bool>> HasLicense()
    {
        return Ok(!string.IsNullOrEmpty(
            (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey)).Value));
    }

    /// <summary>
    /// Asks Kavita+ for the latest license info
    /// </summary>
    /// <param name="forceCheck">Force checking the API and skip the 8-hour cache</param>
    /// <returns></returns>
    [HttpGet("info")]
    [Authorize(PolicyGroups.AdminPolicy)]
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
    [HttpDelete]
    [Authorize(PolicyGroups.AdminPolicy)]
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


    /// <summary>
    /// Break the registration between Kavita+ and this instance
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("reset")]
    [Authorize(PolicyGroups.AdminPolicy)]
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
    [HttpPost]
    [Authorize(PolicyGroups.AdminPolicy)]
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
