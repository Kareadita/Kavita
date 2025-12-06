using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.DTOs;
using API.DTOs.Email;
using API.DTOs.KavitaPlus.Metadata;
using API.DTOs.Settings;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers.Converters;
using API.Services;
using AutoMapper;
using Kavita.Common;
using Kavita.Common.Extensions;
using Kavita.Common.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers;

#nullable enable

public class SettingsController : BaseApiController
{
    private readonly ILogger<SettingsController> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IEmailService _emailService;
    private readonly ILocalizationService _localizationService;
    private readonly ISettingsService _settingsService;
    private readonly IAuthenticationSchemeProvider _authenticationSchemeProvider;

    public SettingsController(ILogger<SettingsController> logger, IUnitOfWork unitOfWork, IMapper mapper,
        IEmailService emailService, ILocalizationService localizationService, ISettingsService settingsService,
        IAuthenticationSchemeProvider authenticationSchemeProvider)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _emailService = emailService;
        _localizationService = localizationService;
        _settingsService = settingsService;
        _authenticationSchemeProvider = authenticationSchemeProvider;
    }

    /// <summary>
    /// Returns the base url for this instance (if set)
    /// </summary>
    /// <returns></returns>
    [HttpGet("base-url")]
    public async Task<ActionResult<string>> GetBaseUrl()
    {
        var settingsDto = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        return Ok(settingsDto.BaseUrl);
    }

    /// <summary>
    /// Returns the server settings
    /// </summary>
    /// <returns></returns>
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [HttpGet]
    public async Task<ActionResult<ServerSettingDto>> GetSettings()
    {
        var settingsDto = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();

        // Do not send OIDC secret to user
        settingsDto.OidcConfig.Secret = "*".Repeat(settingsDto.OidcConfig.Secret.Length);
        return Ok(settingsDto);
    }

    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [HttpPost("reset")]
    public async Task<ActionResult<ServerSettingDto>> ResetSettings()
    {
        _logger.LogInformation("{UserName} is resetting Server Settings", Username!);

        return await UpdateSettings(_mapper.Map<ServerSettingDto>(Seed.DefaultSettings));
    }

    /// <summary>
    /// Resets the IP Addresses
    /// </summary>
    /// <returns></returns>
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [HttpPost("reset-ip-addresses")]
    public async Task<ActionResult<ServerSettingDto>> ResetIpAddressesSettings()
    {
        _logger.LogInformation("{UserName} is resetting IP Addresses Setting", Username!);
        var ipAddresses = await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.IpAddresses);
        ipAddresses.Value = Configuration.DefaultIpAddresses;
        _unitOfWork.SettingsRepository.Update(ipAddresses);

        if (!await _unitOfWork.CommitAsync())
        {
            await _unitOfWork.RollbackAsync();
        }

        return Ok(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync());
    }

    /// <summary>
    /// Resets the Base url
    /// </summary>
    /// <returns></returns>
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [HttpPost("reset-base-url")]
    public async Task<ActionResult<ServerSettingDto>> ResetBaseUrlSettings()
    {
        _logger.LogInformation("{UserName} is resetting Base Url Setting", Username!);
        var baseUrl = await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BaseUrl);
        baseUrl.Value = Configuration.DefaultBaseUrl;
        _unitOfWork.SettingsRepository.Update(baseUrl);

        if (!await _unitOfWork.CommitAsync())
        {
            await _unitOfWork.RollbackAsync();
        }

        Configuration.BaseUrl = baseUrl.Value;
        return Ok(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync());
    }

    /// <summary>
    /// Is the minimum information setup for Email to work
    /// </summary>
    /// <returns></returns>
    [HttpGet("is-email-setup")]
    public async Task<ActionResult<bool>> IsEmailSetup()
    {
        var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        return Ok(settings.IsEmailSetup());
    }


    /// <summary>
    /// Update Server settings
    /// </summary>
    /// <param name="updateSettingsDto"></param>
    /// <returns></returns>
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [HttpPost]
    public async Task<ActionResult<ServerSettingDto>> UpdateSettings(ServerSettingDto updateSettingsDto)
    {
        _logger.LogInformation("{UserName} is updating Server Settings", Username!);

        try
        {
            var d = await _settingsService.UpdateSettings(updateSettingsDto);
            return Ok(d);
        }
        catch (KavitaException ex)
        {
            return BadRequest(await _localizationService.Translate(UserId, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an exception when updating server settings");
            return BadRequest(await _localizationService.Translate(UserId, "generic-error"));
        }
    }

    /// <summary>
    /// All values allowed for Task Scheduling APIs. A custom cron job is not included. Disabled is not applicable for Cleanup.
    /// </summary>
    /// <returns></returns>
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [HttpGet("task-frequencies")]
    public ActionResult<IEnumerable<string>> GetTaskFrequencies()
    {
        return Ok(CronConverter.Options);
    }

    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [HttpGet("library-types")]
    public ActionResult<IEnumerable<string>> GetLibraryTypes()
    {
        return Ok(Enum.GetValues<LibraryType>().Select(t => t.ToDescription()));
    }

    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [HttpGet("log-levels")]
    public ActionResult<IEnumerable<string>> GetLogLevels()
    {
        return Ok(new[] {"Trace", "Debug", "Information", "Warning", "Critical"});
    }

    [HttpGet("opds-enabled")]
    public async Task<ActionResult<bool>> GetOpdsEnabled()
    {
        var settingsDto = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
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
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [HttpPost("test-email-url")]
    public async Task<ActionResult<EmailTestResultDto>> TestEmailServiceUrl()
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(UserId);
        if (string.IsNullOrEmpty(user?.Email)) return BadRequest("Your account has no email on record. Cannot email.");
        return Ok(await _emailService.SendTestEmail(user!.Email));
    }

    /// <summary>
    /// Get the metadata settings for Kavita+ users.
    /// </summary>
    /// <returns></returns>
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [HttpGet("metadata-settings")]
    public async Task<ActionResult<MetadataSettingsDto>> GetMetadataSettings()
    {
        return Ok(await _unitOfWork.SettingsRepository.GetMetadataSettingDto());

    }

    /// <summary>
    /// Update the metadata settings for Kavita+ Metadata feature
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [HttpPost("metadata-settings")]
    public async Task<ActionResult<MetadataSettingsDto>> UpdateMetadataSettings(MetadataSettingsDto dto)
    {
        try
        {
            return Ok(await _settingsService.UpdateMetadataSettings(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an issue when updating metadata settings");
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Import field mappings
    /// </summary>
    /// <returns></returns>
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [HttpPost("import-field-mappings")]
    public async Task<ActionResult<FieldMappingsImportResultDto>> ImportFieldMappings([FromBody] ImportFieldMappingsDto dto)
    {
        try
        {
            return Ok(await _settingsService.ImportFieldMappings(dto.Data, dto.Settings));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an issue importing field mappings");
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
        var oidcScheme = await _authenticationSchemeProvider.GetSchemeAsync(IdentityServiceExtensions.OpenIdConnect);

        var settings = (await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).OidcConfig;
        var publicConfig = _mapper.Map<OidcPublicConfigDto>(settings);
        publicConfig.Enabled = oidcScheme != null &&
                               !string.IsNullOrEmpty(settings.Authority) &&
                               !string.IsNullOrEmpty(settings.ClientId) &&
                               !string.IsNullOrEmpty(settings.Secret);

        return Ok(publicConfig);
    }

    /// <summary>
    /// Return the initial installDate of the server
    /// </summary>
    /// <returns></returns>
    [HttpGet("first-install-date")]
    public async Task<ActionResult<DateTime>> GetInstallDate()
    {
        var installDate = (await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).FirstInstallDate;
        return Ok(installDate);
    }

    /// <summary>
    /// Validate if the given authority is reachable from the server
    /// </summary>
    /// <param name="authority"></param>
    /// <returns></returns>
    [Authorize(PolicyGroups.AdminPolicy)]
    [HttpPost("is-valid-authority")]
    public async Task<ActionResult<bool>> IsValidAuthority([FromBody] AuthorityValidationDto authority)
    {
        return Ok(await _settingsService.IsValidAuthority(authority.Authority));
    }


}
