using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EasyCaching.Core;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.Plus;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.KavitaPlus;
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
    IEasyCachingProviderFactory cachingProviderFactory,
    IKavitaPlusProviderHealthService providerHealthService)
    : BaseApiController
{
    /// <summary>
    /// Checks if the user's license is valid or not
    /// </summary>
    /// <returns></returns>
    [HttpGet("valid-license")]
    public async Task<ActionResult<bool>> HasValidLicense(bool forceCheck = false)
    {
        var ct = HttpContext.RequestAborted;
        var result = await licenseService.HasActiveLicense(forceCheck, ct);

        var licenseInfoProvider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.License);
        var cacheValue = await licenseInfoProvider.GetAsync<bool>(LicenseService.CacheKey, ct);

        if (result && !cacheValue.IsNull && !cacheValue.Value)
        {
            await taskScheduler.ScheduleKavitaPlusTasks(ct);
        }

        return Ok(result);
    }

    /// <summary>
    /// Has any license registered with the instance. Does not validate against Kavita+ API
    /// </summary>
    /// <returns></returns>
    [HttpGet("has-license")]
    public async Task<ActionResult<bool>> HasLicense()
    {
        var ct = HttpContext.RequestAborted;
        return Ok(!string.IsNullOrEmpty(
            (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct)).Value));
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
        var ct = HttpContext.RequestAborted;
        try
        {
            return Ok(await licenseService.GetLicenseInfo(forceCheck, ct));
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
        var ct = HttpContext.RequestAborted;
        logger.LogInformation("Removing license on file for Server");
        var setting = await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct);
        setting.Value = null;
        unitOfWork.SettingsRepository.Update(setting);
        await unitOfWork.CommitAsync(ct);

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
        var ct = HttpContext.RequestAborted;
        logger.LogInformation("Resetting license on file for Server");
        if (await licenseService.ResetLicense(dto.License, dto.Email, ct))
        {
            await taskScheduler.ScheduleKavitaPlusTasks(ct);
            return Ok();
        }

        return BadRequest(await localizationService.TranslateAsync(UserId, "unable-to-reset-k+"));
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
    /// <remarks>Caches the result when successful</remarks>
    /// <returns></returns>
    [HttpPost]
    [Authorize(PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<KavitaPlusRegisterResultDto>> UpdateLicense(UpdateLicenseDto dto)
    {
        var result = await licenseService.AddLicense(dto.License.Trim(), dto.Email.Trim(), dto.DiscordId?.Trim(), HttpContext.RequestAborted);
        if (result.Success)
        {
            await taskScheduler.ScheduleKavitaPlusTasks();
        }
        return Ok(result);
    }

    /// <summary>
    /// Provides a 15 min snapshot of Kavita+ Providers (Hardcover, AniList, MangaBaka, etc.) API health.
    /// Kavita caches every 45 mins.
    /// </summary>
    /// <param name="forceCheck">Bypass cache and force a reload from Kavita+ server</param>
    /// <returns></returns>
    [HttpGet("provider-health")]
    public async Task<ActionResult<IList<KavitaPlusProviderHealthSnapshotDto>>> GetProviderHealthSnapshot(bool forceCheck = false)
    {
        var ct = HttpContext.RequestAborted;
        return Ok(await providerHealthService.GetProviderHealthSnapshot(forceCheck, ct));
    }

    /// <summary>
    /// Providers how many interactions this license has had with Kavita+ over a lifetime
    /// </summary>
    /// <returns></returns>
    [HttpGet("stats")]
    public async Task<ActionResult<KavitaPlusLicenseUsageDto>> GetLicenseUsage()
    {
        var ct = HttpContext.RequestAborted;
        return Ok(await licenseService.GetLicenseUsage(ct));
    }

    /// <summary>
    /// Cancels the active Kavita+ Subscription tied to this server. License will elapse at end of billing period.
    /// </summary>
    /// <returns></returns>
    [HttpDelete("cancel")]
    [Authorize(PolicyGroups.AdminPolicy)]
    public async Task<ActionResult> CancelLicense([FromBody] CancelKavitaPlusLicenseDto dto)
    {
        var ct = HttpContext.RequestAborted;
        if (await licenseService.CancelLicense(dto, ct)) return Ok();
        return BadRequest(await localizationService.TranslateAsync(UserId, "unable-to-cancel-k+"));
    }

    /// <summary>
    /// Returns the available Kavita+ products (billing interval and list price) the renew flow can select.
    /// </summary>
    /// <returns></returns>
    [HttpGet("products")]
    [Authorize(PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<IList<KavitaPlusProductInfoDto>>> GetProducts()
    {
        var ct = HttpContext.RequestAborted;
        return Ok(await licenseService.GetProducts(ct));
    }

    /// <summary>
    /// Renews the subscription on the given billing interval and returns the Stripe Checkout (Pay Now) URL the customer visits to pay.
    /// </summary>
    /// <returns>The Stripe Checkout URL</returns>
    [HttpPost("renew")]
    [Authorize(PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<string>> RenewLicense(RenewKavitaPlusLicenseDto dto)
    {
        var ct = HttpContext.RequestAborted;
        var checkoutUrl = await licenseService.RenewLicense(dto, ct);
        if (!string.IsNullOrEmpty(checkoutUrl)) return Ok(checkoutUrl);
        return BadRequest(await localizationService.TranslateAsync(UserId, "unable-to-renew-k+"));
    }

    /// <summary>
    /// Change email on Kavita+ License/Stripe - Sends a confirmation email on Kavita+ side. No-op for server.
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("change-email")]
    [Authorize(PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<bool>> ChangeEmail(ChangeEmailOnLicenseDto dto)
    {
        var ct = HttpContext.RequestAborted;
        var success = await licenseService.ChangeEmail(dto, ct);
        return Ok(success);
    }

}
