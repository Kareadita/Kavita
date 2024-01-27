using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Email;
using API.DTOs.Settings;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers.Converters;
using API.Logging;
using API.Services;
using API.Services.Tasks.Scanner;
using AutoMapper;
using Cronos;
using Hangfire;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Kavita.Common.Extensions;
using Kavita.Common.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers;

#nullable enable

public class SettingsController : BaseApiController
{
    private readonly ILogger<SettingsController> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITaskScheduler _taskScheduler;
    private readonly IDirectoryService _directoryService;
    private readonly IMapper _mapper;
    private readonly IEmailService _emailService;
    private readonly ILibraryWatcher _libraryWatcher;
    private readonly ILocalizationService _localizationService;

    public SettingsController(ILogger<SettingsController> logger, IUnitOfWork unitOfWork, ITaskScheduler taskScheduler,
        IDirectoryService directoryService, IMapper mapper, IEmailService emailService, ILibraryWatcher libraryWatcher,
        ILocalizationService localizationService)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _taskScheduler = taskScheduler;
        _directoryService = directoryService;
        _mapper = mapper;
        _emailService = emailService;
        _libraryWatcher = libraryWatcher;
        _localizationService = localizationService;
    }

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
    [Authorize(Policy = "RequireAdminRole")]
    [HttpGet]
    public async Task<ActionResult<ServerSettingDto>> GetSettings()
    {
        var settingsDto = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        return Ok(settingsDto);
    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("reset")]
    public async Task<ActionResult<ServerSettingDto>> ResetSettings()
    {
        _logger.LogInformation("{UserName} is resetting Server Settings", User.GetUsername());

        return await UpdateSettings(_mapper.Map<ServerSettingDto>(Seed.DefaultSettings));
    }

    /// <summary>
    /// Resets the IP Addresses
    /// </summary>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("reset-ip-addresses")]
    public async Task<ActionResult<ServerSettingDto>> ResetIpAddressesSettings()
    {
        _logger.LogInformation("{UserName} is resetting IP Addresses Setting", User.GetUsername());
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
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("reset-base-url")]
    public async Task<ActionResult<ServerSettingDto>> ResetBaseUrlSettings()
    {
        _logger.LogInformation("{UserName} is resetting Base Url Setting", User.GetUsername());
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
    [Authorize]
    [HttpGet("is-email-setup")]
    public async Task<ActionResult<bool>> IsEmailSetup()
    {
        var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        return Ok(settings.IsEmailSetup());
    }



    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost]
    public async Task<ActionResult<ServerSettingDto>> UpdateSettings(ServerSettingDto updateSettingsDto)
    {
        _logger.LogInformation("{UserName} is updating Server Settings", User.GetUsername());

        // We do not allow CacheDirectory changes, so we will ignore.
        var currentSettings = await _unitOfWork.SettingsRepository.GetSettingsAsync();
        var updateBookmarks = false;
        var originalBookmarkDirectory = _directoryService.BookmarkDirectory;

        var bookmarkDirectory = updateSettingsDto.BookmarksDirectory;
        if (!updateSettingsDto.BookmarksDirectory.EndsWith("bookmarks") &&
            !updateSettingsDto.BookmarksDirectory.EndsWith("bookmarks/"))
        {
            bookmarkDirectory =
                _directoryService.FileSystem.Path.Join(updateSettingsDto.BookmarksDirectory, "bookmarks");
        }

        if (string.IsNullOrEmpty(updateSettingsDto.BookmarksDirectory))
        {
            bookmarkDirectory = _directoryService.BookmarkDirectory;
        }

        foreach (var setting in currentSettings)
        {
            UpdateSchedulingSettings(setting, updateSettingsDto);

            if (setting.Key == ServerSettingKey.OnDeckProgressDays &&
                updateSettingsDto.OnDeckProgressDays + string.Empty != setting.Value)
            {
                setting.Value = updateSettingsDto.OnDeckProgressDays + string.Empty;
                _unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.OnDeckUpdateDays &&
                updateSettingsDto.OnDeckUpdateDays + string.Empty != setting.Value)
            {
                setting.Value = updateSettingsDto.OnDeckUpdateDays + string.Empty;
                _unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.CoverImageSize &&
                updateSettingsDto.CoverImageSize + string.Empty != setting.Value)
            {
                setting.Value = updateSettingsDto.CoverImageSize + string.Empty;
                _unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.Port && updateSettingsDto.Port + string.Empty != setting.Value)
            {
                if (OsInfo.IsDocker) continue;
                setting.Value = updateSettingsDto.Port + string.Empty;
                // Port is managed in appSetting.json
                Configuration.Port = updateSettingsDto.Port;
                _unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.CacheSize &&
                updateSettingsDto.CacheSize + string.Empty != setting.Value)
            {
                setting.Value = updateSettingsDto.CacheSize + string.Empty;
                // CacheSize is managed in appSetting.json
                Configuration.CacheSize = updateSettingsDto.CacheSize;
                _unitOfWork.SettingsRepository.Update(setting);
            }

            UpdateEmailSettings(setting, updateSettingsDto);



            if (setting.Key == ServerSettingKey.IpAddresses && updateSettingsDto.IpAddresses != setting.Value)
            {
                if (OsInfo.IsDocker) continue;
                // Validate IP addresses
                foreach (var ipAddress in updateSettingsDto.IpAddresses.Split(',',
                             StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!IPAddress.TryParse(ipAddress.Trim(), out _))
                    {
                        return BadRequest(await _localizationService.Translate(User.GetUserId(), "ip-address-invalid",
                            ipAddress));
                    }
                }

                setting.Value = updateSettingsDto.IpAddresses;
                // IpAddresses is managed in appSetting.json
                Configuration.IpAddresses = updateSettingsDto.IpAddresses;
                _unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.BaseUrl && updateSettingsDto.BaseUrl + string.Empty != setting.Value)
            {
                var path = !updateSettingsDto.BaseUrl.StartsWith('/')
                    ? $"/{updateSettingsDto.BaseUrl}"
                    : updateSettingsDto.BaseUrl;
                path = !path.EndsWith('/')
                    ? $"{path}/"
                    : path;
                setting.Value = path;
                Configuration.BaseUrl = updateSettingsDto.BaseUrl;
                _unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.LoggingLevel &&
                updateSettingsDto.LoggingLevel + string.Empty != setting.Value)
            {
                setting.Value = updateSettingsDto.LoggingLevel + string.Empty;
                LogLevelOptions.SwitchLogLevel(updateSettingsDto.LoggingLevel);
                _unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.EnableOpds &&
                updateSettingsDto.EnableOpds + string.Empty != setting.Value)
            {
                setting.Value = updateSettingsDto.EnableOpds + string.Empty;
                _unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.EncodeMediaAs &&
                updateSettingsDto.EncodeMediaAs + string.Empty != setting.Value)
            {
                setting.Value = updateSettingsDto.EncodeMediaAs + string.Empty;
                _unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.HostName && updateSettingsDto.HostName + string.Empty != setting.Value)
            {
                setting.Value = (updateSettingsDto.HostName + string.Empty).Trim();
                setting.Value = UrlHelper.RemoveEndingSlash(setting.Value);
                _unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.BookmarkDirectory && bookmarkDirectory != setting.Value)
            {
                // Validate new directory can be used
                if (!await _directoryService.CheckWriteAccess(bookmarkDirectory))
                {
                    return BadRequest(
                        await _localizationService.Translate(User.GetUserId(), "bookmark-dir-permissions"));
                }

                originalBookmarkDirectory = setting.Value;
                // Normalize the path deliminators. Just to look nice in DB, no functionality
                setting.Value = _directoryService.FileSystem.Path.GetFullPath(bookmarkDirectory);
                _unitOfWork.SettingsRepository.Update(setting);
                updateBookmarks = true;

            }

            if (setting.Key == ServerSettingKey.AllowStatCollection &&
                updateSettingsDto.AllowStatCollection + string.Empty != setting.Value)
            {
                setting.Value = updateSettingsDto.AllowStatCollection + string.Empty;
                _unitOfWork.SettingsRepository.Update(setting);
                if (!updateSettingsDto.AllowStatCollection)
                {
                    _taskScheduler.CancelStatsTasks();
                }
                else
                {
                    await _taskScheduler.ScheduleStatsTasks();
                }
            }

            if (setting.Key == ServerSettingKey.TotalBackups &&
                updateSettingsDto.TotalBackups + string.Empty != setting.Value)
            {
                if (updateSettingsDto.TotalBackups > 30 || updateSettingsDto.TotalBackups < 1)
                {
                    return BadRequest(await _localizationService.Translate(User.GetUserId(), "total-backups"));
                }

                setting.Value = updateSettingsDto.TotalBackups + string.Empty;
                _unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.TotalLogs &&
                updateSettingsDto.TotalLogs + string.Empty != setting.Value)
            {
                if (updateSettingsDto.TotalLogs > 30 || updateSettingsDto.TotalLogs < 1)
                {
                    return BadRequest(await _localizationService.Translate(User.GetUserId(), "total-logs"));
                }

                setting.Value = updateSettingsDto.TotalLogs + string.Empty;
                _unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.EnableFolderWatching &&
                updateSettingsDto.EnableFolderWatching + string.Empty != setting.Value)
            {
                setting.Value = updateSettingsDto.EnableFolderWatching + string.Empty;
                _unitOfWork.SettingsRepository.Update(setting);
            }
        }

        if (!_unitOfWork.HasChanges()) return Ok(updateSettingsDto);

        try
        {
            await _unitOfWork.CommitAsync();

            if (updateBookmarks)
            {
                _directoryService.ExistOrCreate(bookmarkDirectory);
                _directoryService.CopyDirectoryToDirectory(originalBookmarkDirectory, bookmarkDirectory);
                _directoryService.ClearAndDeleteDirectory(originalBookmarkDirectory);
            }

            if (updateSettingsDto.EnableFolderWatching)
            {
                await _libraryWatcher.StartWatching();
            }
            else
            {
                _libraryWatcher.StopWatching();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an exception when updating server settings");
            await _unitOfWork.RollbackAsync();
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-error"));
        }


        _logger.LogInformation("Server Settings updated");
        await _taskScheduler.ScheduleTasks();
        return Ok(updateSettingsDto);
    }

    private void UpdateSchedulingSettings(ServerSetting setting, ServerSettingDto updateSettingsDto)
    {
        if (setting.Key == ServerSettingKey.TaskBackup && updateSettingsDto.TaskBackup != setting.Value)
        {
            setting.Value = updateSettingsDto.TaskBackup;
            _unitOfWork.SettingsRepository.Update(setting);
        }

        if (setting.Key == ServerSettingKey.TaskScan && updateSettingsDto.TaskScan != setting.Value)
        {
            setting.Value = updateSettingsDto.TaskScan;
            _unitOfWork.SettingsRepository.Update(setting);
        }

        if (setting.Key == ServerSettingKey.TaskCleanup && updateSettingsDto.TaskCleanup != setting.Value)
        {
            setting.Value = updateSettingsDto.TaskCleanup;
            _unitOfWork.SettingsRepository.Update(setting);
        }
    }

    private void UpdateEmailSettings(ServerSetting setting, ServerSettingDto updateSettingsDto)
    {
        if (setting.Key == ServerSettingKey.EmailHost &&
            updateSettingsDto.SmtpConfig.Host + string.Empty != setting.Value)
        {
            setting.Value = updateSettingsDto.SmtpConfig.Host + string.Empty;
            _unitOfWork.SettingsRepository.Update(setting);
        }

        if (setting.Key == ServerSettingKey.EmailPort &&
            updateSettingsDto.SmtpConfig.Port + string.Empty != setting.Value)
        {
            setting.Value = updateSettingsDto.SmtpConfig.Port + string.Empty;
            _unitOfWork.SettingsRepository.Update(setting);
        }

        if (setting.Key == ServerSettingKey.EmailAuthPassword &&
            updateSettingsDto.SmtpConfig.Password + string.Empty != setting.Value)
        {
            setting.Value = updateSettingsDto.SmtpConfig.Password + string.Empty;
            _unitOfWork.SettingsRepository.Update(setting);
        }

        if (setting.Key == ServerSettingKey.EmailAuthUserName &&
            updateSettingsDto.SmtpConfig.UserName + string.Empty != setting.Value)
        {
            setting.Value = updateSettingsDto.SmtpConfig.UserName + string.Empty;
            _unitOfWork.SettingsRepository.Update(setting);
        }

        if (setting.Key == ServerSettingKey.EmailSenderAddress &&
            updateSettingsDto.SmtpConfig.SenderAddress + string.Empty != setting.Value)
        {
            setting.Value = updateSettingsDto.SmtpConfig.SenderAddress + string.Empty;
            _unitOfWork.SettingsRepository.Update(setting);
        }

        if (setting.Key == ServerSettingKey.EmailSenderDisplayName &&
            updateSettingsDto.SmtpConfig.SenderDisplayName + string.Empty != setting.Value)
        {
            setting.Value = updateSettingsDto.SmtpConfig.SenderDisplayName + string.Empty;
            _unitOfWork.SettingsRepository.Update(setting);
        }

        if (setting.Key == ServerSettingKey.EmailSizeLimit &&
            updateSettingsDto.SmtpConfig.SizeLimit + string.Empty != setting.Value)
        {
            setting.Value = updateSettingsDto.SmtpConfig.SizeLimit + string.Empty;
            _unitOfWork.SettingsRepository.Update(setting);
        }

        if (setting.Key == ServerSettingKey.EmailEnableSsl &&
            updateSettingsDto.SmtpConfig.EnableSsl + string.Empty != setting.Value)
        {
            setting.Value = updateSettingsDto.SmtpConfig.EnableSsl + string.Empty;
            _unitOfWork.SettingsRepository.Update(setting);
        }

        if (setting.Key == ServerSettingKey.EmailCustomizedTemplates &&
            updateSettingsDto.SmtpConfig.CustomizedTemplates + string.Empty != setting.Value)
        {
            setting.Value = updateSettingsDto.SmtpConfig.CustomizedTemplates + string.Empty;
            _unitOfWork.SettingsRepository.Update(setting);
        }
    }

    /// <summary>
    /// All values allowed for Task Scheduling APIs. A custom cron job is not included. Disabled is not applicable for Cleanup.
    /// </summary>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpGet("task-frequencies")]
    public ActionResult<IEnumerable<string>> GetTaskFrequencies()
    {
        return Ok(CronConverter.Options);
    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpGet("library-types")]
    public ActionResult<IEnumerable<string>> GetLibraryTypes()
    {
        return Ok(Enum.GetValues<LibraryType>().Select(t => t.ToDescription()));
    }

    [Authorize(Policy = "RequireAdminRole")]
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
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("test-email-url")]
    public async Task<ActionResult<EmailTestResultDto>> TestEmailServiceUrl()
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(User.GetUserId());
        return Ok(await _emailService.SendTestEmail(user!.Email));
    }
}
