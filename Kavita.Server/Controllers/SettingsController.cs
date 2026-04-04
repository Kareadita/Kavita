using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Common;
using Kavita.Common.Extensions;
using Kavita.Common.Helpers;
using Kavita.Models;
using Kavita.Models.Constants;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Email;
using Kavita.Models.DTOs.KavitaPlus.Metadata;
using Kavita.Models.DTOs.Settings;
using Kavita.Models.Entities.Enums;
using Kavita.Server.Extensions;
using Kavita.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.Controllers;

public class SettingsController(
    ILogger<SettingsController> logger,
    IUnitOfWork unitOfWork,
    IMapper mapper,
    IEmailService emailService,
    ILocalizationService localizationService,
    ISettingsService settingsService,
    IAuthenticationSchemeProvider authenticationSchemeProvider,
    IOidcService oidcService)
    : BaseApiController
{
    /// <summary>
    /// Returns the base url for this instance (if set)
    /// </summary>
    /// <returns></returns>
    [HttpGet("base-url")]
    public async Task<ActionResult<string>> GetBaseUrl()
    {
        var settingsDto = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        return Ok(settingsDto.BaseUrl);
    }

    /// <summary>
    /// Returns the server settings
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<ServerSettingDto>> GetSettings()
    {
        var settingsDto = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();

        // Do not send OIDC secret to user
        settingsDto.OidcConfig.Secret = "*".Repeat(settingsDto.OidcConfig.Secret.Length);
        return Ok(settingsDto);
    }

    [HttpPost("reset")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<ServerSettingDto>> ResetSettings()
    {
        logger.LogInformation("{UserName} is resetting Server Settings", Username!);

        return await UpdateSettings(mapper.Map<ServerSettingDto>(Defaults.DefaultSettings));
    }

    /// <summary>
    /// Resets the IP Addresses
    /// </summary>
    /// <returns></returns>
    [HttpPost("reset-ip-addresses")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<ServerSettingDto>> ResetIpAddressesSettings()
    {
        logger.LogInformation("{UserName} is resetting IP Addresses Setting", Username!);
        var ipAddresses = await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.IpAddresses);
        ipAddresses.Value = Configuration.DefaultIpAddresses;
        unitOfWork.SettingsRepository.Update(ipAddresses);

        if (!await unitOfWork.CommitAsync())
        {
            await unitOfWork.RollbackAsync();
        }

        return Ok(await unitOfWork.SettingsRepository.GetSettingsDtoAsync());
    }

    /// <summary>
    /// Resets the Base url
    /// </summary>
    /// <returns></returns>
    [HttpPost("reset-base-url")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<ServerSettingDto>> ResetBaseUrlSettings()
    {
        logger.LogInformation("{UserName} is resetting Base Url Setting", Username!);
        var baseUrl = await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BaseUrl);
        baseUrl.Value = Configuration.DefaultBaseUrl;
        unitOfWork.SettingsRepository.Update(baseUrl);

        if (!await unitOfWork.CommitAsync())
        {
            await unitOfWork.RollbackAsync();
        }

        Configuration.BaseUrl = baseUrl.Value;
        return Ok(await unitOfWork.SettingsRepository.GetSettingsDtoAsync());
    }

    /// <summary>
    /// Is the minimum information setup for Email to work
    /// </summary>
    /// <returns></returns>
    [HttpGet("is-email-setup")]
    public async Task<ActionResult<bool>> IsEmailSetup()
    {
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        return Ok(settings.IsEmailSetup());
    }


    /// <summary>
    /// Update Server settings
    /// </summary>
    /// <param name="updateSettingsDto"></param>
    /// <returns></returns>
    [HttpPost]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<ServerSettingDto>> UpdateSettings(ServerSettingDto updateSettingsDto)
    {
        logger.LogInformation("{UserName} is updating Server Settings", Username!);

        try
        {
            var d = await settingsService.UpdateSettings(updateSettingsDto);
            return Ok(d);
        }
        catch (KavitaException ex)
        {
            return BadRequest(await localizationService.Translate(UserId, ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an exception when updating server settings");
            return BadRequest(await localizationService.Translate(UserId, "generic-error"));
        }
    }

    /// <summary>
    /// All values allowed for Task Scheduling APIs. A custom cron job is not included. Disabled is not applicable for Cleanup.
    /// </summary>
    /// <returns></returns>
    [HttpGet("task-frequencies")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public ActionResult<IEnumerable<string>> GetTaskFrequencies()
    {
        return Ok(CronConverter.Options);
    }

    [HttpGet("library-types")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public ActionResult<IEnumerable<string>> GetLibraryTypes()
    {
        return Ok(Enum.GetValues<LibraryType>().Select(t => t.ToDescription()));
    }

    [HttpGet("log-levels")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public ActionResult<IEnumerable<string>> GetLogLevels()
    {
        return Ok(new[] {"Trace", "Debug", "Information", "Warning", "Critical"});
    }

    [HttpGet("opds-enabled")]
    public async Task<ActionResult<bool>> GetOpdsEnabled()
    {
        var settingsDto = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        return Ok(settingsDto.EnableOpds);
    }

    /// <summary>
    /// Is the cron expression valid for Kavita's scheduler
    /// </summary>
    /// <param name="cronExpression"></param>
    /// <returns></returns>
    [HttpGet("is-valid-cron")]
    public ActionResult<bool> IsValidCron(string cronExpression)
    {
        // NOTE: This must match Hangfire's underlying cron system. Hangfire is unique
        return Ok(CronHelper.IsValidCron(cronExpression));
    }

    /// <summary>
    /// Sends a test email to see if email settings are hooked up correctly
    /// </summary>
    /// <returns></returns>
    [HttpPost("test-email-url")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<EmailTestResultDto>> TestEmailServiceUrl()
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(UserId);
        if (string.IsNullOrEmpty(user?.Email)) return BadRequest("Your account has no email on record. Cannot email.");
        return Ok(await emailService.SendTestEmail(user!.Email));
    }

    /// <summary>
    /// Get the metadata settings for Kavita+ users.
    /// </summary>
    /// <returns></returns>
    [HttpGet("metadata-settings")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<MetadataSettingsDto>> GetMetadataSettings()
    {
        return Ok(await unitOfWork.SettingsRepository.GetMetadataSettingDto());

    }

    /// <summary>
    /// Update the metadata settings for Kavita+ Metadata feature
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("metadata-settings")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<MetadataSettingsDto>> UpdateMetadataSettings(MetadataSettingsDto dto)
    {
        try
        {
            return Ok(await settingsService.UpdateMetadataSettings(dto));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an issue when updating metadata settings");
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Import field mappings
    /// </summary>
    /// <returns></returns>
    [HttpPost("import-field-mappings")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<FieldMappingsImportResultDto>> ImportFieldMappings([FromBody] ImportFieldMappingsDto dto)
    {
        try
        {
            return Ok(await settingsService.ImportFieldMappings(dto.Data, dto.Settings));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an issue importing field mappings");
            return BadRequest(ex.Message);
        }
    }


    /// <summary>
    /// Retrieve publicly required configuration regarding Oidc
    /// </summary>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpGet("oidc")]
    public async Task<ActionResult<OidcPublicConfigDto>> GetOidcConfig()
    {
        var oidcScheme = await authenticationSchemeProvider.GetSchemeAsync(IdentityServiceExtensions.OpenIdConnect);

        var settings = (await unitOfWork.SettingsRepository.GetSettingsDtoAsync()).OidcConfig;
        var publicConfig = mapper.Map<OidcPublicConfigDto>(settings);
        publicConfig.Enabled = oidcScheme != null &&
                               !string.IsNullOrEmpty(settings.Authority) &&
                               !string.IsNullOrEmpty(settings.ClientId) &&
                               !string.IsNullOrEmpty(settings.Secret);

        return Ok(publicConfig);
    }

    [Authorize(PolicyGroups.AdminPolicy)]
    [HttpPost("reset-external-ids")]
    public async Task<IActionResult> ResetExternalIds()
    {
        await oidcService.ClearOidcIds();

        return Ok();
    }

    /// <summary>
    /// Validate if the given authority is reachable from the server
    /// </summary>
    /// <param name="authority"></param>
    /// <returns></returns>
    [Authorize(PolicyGroups.AdminPolicy)]
    [HttpPost("is-valid-authority")]
    public async Task<ActionResult<AuthorityValidationResult>> IsValidAuthority([FromBody] AuthorityValidationDto authority)
    {
        return Ok(await settingsService.IsValidAuthority(authority.Authority));
    }


}
