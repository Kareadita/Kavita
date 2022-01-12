using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Settings;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers.Converters;
using API.Services;
using AutoMapper;
using Kavita.Common;
using Kavita.Common.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers
{
    public class SettingsController : BaseApiController
    {
        private readonly ILogger<SettingsController> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITaskScheduler _taskScheduler;
        private readonly IAccountService _accountService;
        private readonly IDirectoryService _directoryService;
        private readonly IMapper _mapper;

        public SettingsController(ILogger<SettingsController> logger, IUnitOfWork unitOfWork, ITaskScheduler taskScheduler,
            IAccountService accountService, IDirectoryService directoryService, IMapper mapper)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _taskScheduler = taskScheduler;
            _accountService = accountService;
            _directoryService = directoryService;
            _mapper = mapper;
        }

        [AllowAnonymous]
        [HttpGet("base-url")]
        public async Task<ActionResult<string>> GetBaseUrl()
        {
            var settingsDto = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
            return Ok(settingsDto.BaseUrl);
        }

        [Authorize(Policy = "RequireAdminRole")]
        [HttpGet]
        public async Task<ActionResult<ServerSettingDto>> GetSettings()
        {
            var settingsDto = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
            // TODO: Is this needed as it gets updated in the DB on startup
            settingsDto.Port = Configuration.Port;
            settingsDto.LoggingLevel = Configuration.LogLevel;
            return Ok(settingsDto);
        }

        [Authorize(Policy = "RequireAdminRole")]
        [HttpPost("reset")]
        public async Task<ActionResult<ServerSettingDto>> ResetSettings()
        {
            _logger.LogInformation("{UserName} is resetting Server Settings", User.GetUsername());

            return await UpdateSettings(_mapper.Map<ServerSettingDto>(Seed.DefaultSettings));
        }

        [Authorize(Policy = "RequireAdminRole")]
        [HttpPost]
        public async Task<ActionResult<ServerSettingDto>> UpdateSettings(ServerSettingDto updateSettingsDto)
        {
            _logger.LogInformation("{UserName}  is updating Server Settings", User.GetUsername());

            if (updateSettingsDto.CacheDirectory.Equals(string.Empty))
            {
                return BadRequest("Cache Directory cannot be empty");
            }

            if (!Directory.Exists(updateSettingsDto.CacheDirectory))
            {
                return BadRequest("Directory does not exist or is not accessible.");
            }

            // We do not allow CacheDirectory changes, so we will ignore.
            var currentSettings = await _unitOfWork.SettingsRepository.GetSettingsAsync();
            var updateAuthentication = false;
            var updateBookmarks = false;
            var originalBookmarkDirectory = _directoryService.BookmarkDirectory;

            var bookmarkDirectory = updateSettingsDto.BookmarksDirectory;
            if (!updateSettingsDto.BookmarksDirectory.EndsWith("bookmarks") &&
                !updateSettingsDto.BookmarksDirectory.EndsWith("bookmarks/"))
            {
                bookmarkDirectory = _directoryService.FileSystem.Path.Join(updateSettingsDto.BookmarksDirectory, "bookmarks");
            }

            if (string.IsNullOrEmpty(updateSettingsDto.BookmarksDirectory))
            {
                bookmarkDirectory = _directoryService.BookmarkDirectory;
            }

            foreach (var setting in currentSettings)
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

                if (setting.Key == ServerSettingKey.Port && updateSettingsDto.Port + string.Empty != setting.Value)
                {
                    setting.Value = updateSettingsDto.Port + string.Empty;
                    // Port is managed in appSetting.json
                    Configuration.Port = updateSettingsDto.Port;
                    _unitOfWork.SettingsRepository.Update(setting);
                }

                if (setting.Key == ServerSettingKey.BaseUrl && updateSettingsDto.BaseUrl + string.Empty != setting.Value)
                {
                    var path = !updateSettingsDto.BaseUrl.StartsWith("/")
                        ? $"/{updateSettingsDto.BaseUrl}"
                        : updateSettingsDto.BaseUrl;
                    path = !path.EndsWith("/")
                        ? $"{path}/"
                        : path;
                    setting.Value = path;
                    _unitOfWork.SettingsRepository.Update(setting);
                }

                if (setting.Key == ServerSettingKey.LoggingLevel && updateSettingsDto.LoggingLevel + string.Empty != setting.Value)
                {
                    setting.Value = updateSettingsDto.LoggingLevel + string.Empty;
                    Configuration.LogLevel = updateSettingsDto.LoggingLevel;
                    _unitOfWork.SettingsRepository.Update(setting);
                }

                if (setting.Key == ServerSettingKey.EnableOpds && updateSettingsDto.EnableOpds + string.Empty != setting.Value)
                {
                    setting.Value = updateSettingsDto.EnableOpds + string.Empty;
                    _unitOfWork.SettingsRepository.Update(setting);
                }

                if (setting.Key == ServerSettingKey.BookmarkDirectory && bookmarkDirectory != setting.Value)
                {
                    // Validate new directory can be used
                    if (!await _directoryService.CheckWriteAccess(bookmarkDirectory))
                    {
                        return BadRequest("Bookmark Directory does not have correct permissions for Kavita to use");
                    }

                    originalBookmarkDirectory = setting.Value;
                    // Normalize the path deliminators. Just to look nice in DB, no functionality
                    setting.Value = _directoryService.FileSystem.Path.GetFullPath(bookmarkDirectory);
                    _unitOfWork.SettingsRepository.Update(setting);
                    updateBookmarks = true;

                }

                if (setting.Key == ServerSettingKey.EnableAuthentication && updateSettingsDto.EnableAuthentication + string.Empty != setting.Value)
                {
                    setting.Value = updateSettingsDto.EnableAuthentication + string.Empty;
                    _unitOfWork.SettingsRepository.Update(setting);
                    updateAuthentication = true;
                }

                if (setting.Key == ServerSettingKey.AllowStatCollection && updateSettingsDto.AllowStatCollection + string.Empty != setting.Value)
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
            }

            if (!_unitOfWork.HasChanges()) return Ok(updateSettingsDto);

            try
            {
                await _unitOfWork.CommitAsync();

                if (updateAuthentication)
                {
                    var users = await _unitOfWork.UserRepository.GetNonAdminUsersAsync();
                    foreach (var user in users)
                    {
                        var errors = await _accountService.ChangeUserPassword(user, AccountService.DefaultPassword);
                        if (!errors.Any()) continue;

                        await _unitOfWork.RollbackAsync();
                        return BadRequest(errors);
                    }

                    _logger.LogInformation("Server authentication changed. Updated all non-admins to default password");
                }

                if (updateBookmarks)
                {
                    _directoryService.ExistOrCreate(bookmarkDirectory);
                    _directoryService.CopyDirectoryToDirectory(originalBookmarkDirectory, bookmarkDirectory);
                    _directoryService.ClearAndDeleteDirectory(originalBookmarkDirectory);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an exception when updating server settings");
                await _unitOfWork.RollbackAsync();
                return BadRequest("There was a critical issue. Please try again.");
            }


            _logger.LogInformation("Server Settings updated");
            await _taskScheduler.ScheduleTasks();
            return Ok(updateSettingsDto);
        }

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
            return Ok(new [] {"Trace", "Debug", "Information", "Warning", "Critical"});
        }

        [HttpGet("opds-enabled")]
        public async Task<ActionResult<bool>> GetOpdsEnabled()
        {
            var settingsDto = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
            return Ok(settingsDto.EnableOpds);
        }

        [HttpGet("authentication-enabled")]
        public async Task<ActionResult<bool>> GetAuthenticationEnabled()
        {
            var settingsDto = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
            return Ok(settingsDto.EnableAuthentication);
        }
    }
}
