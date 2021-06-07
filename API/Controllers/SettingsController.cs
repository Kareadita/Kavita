using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers.Converters;
using API.Interfaces;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace API.Controllers
{
    [Authorize]
    public class SettingsController : BaseApiController
    {
        private readonly ILogger<SettingsController> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITaskScheduler _taskScheduler;
        private readonly IConfiguration _configuration;

        public SettingsController(ILogger<SettingsController> logger, IUnitOfWork unitOfWork, ITaskScheduler taskScheduler, IConfiguration configuration)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _taskScheduler = taskScheduler;
            _configuration = configuration;
        }

        [HttpGet("")]
        public async Task<ActionResult<ServerSettingDto>> GetSettings()
        {
            var settingsDto = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
            settingsDto.Port = Configuration.GetPort(Program.GetAppSettingFilename());
            settingsDto.LoggingLevel = Configuration.GetLogLevel(Program.GetAppSettingFilename());
            return Ok(settingsDto);
        }

        [Authorize(Policy = "RequireAdminRole")]
        [HttpPost("")]
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
            
            var logLevelOptions = new LogLevelOptions();
            _configuration.GetSection("Logging:LogLevel").Bind(logLevelOptions);

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
                
                if (setting.Key == ServerSettingKey.Port && updateSettingsDto.Port + "" != setting.Value)
                {
                    setting.Value = updateSettingsDto.Port + "";
                    // Port is managed in appSetting.json
                    Configuration.UpdatePort(Program.GetAppSettingFilename(), updateSettingsDto.Port);
                    _unitOfWork.SettingsRepository.Update(setting);
                }
                
                if (setting.Key == ServerSettingKey.LoggingLevel && updateSettingsDto.LoggingLevel + "" != setting.Value)
                {
                    setting.Value = updateSettingsDto.LoggingLevel + "";
                    Configuration.UpdateLogLevel(Program.GetAppSettingFilename(), updateSettingsDto.LoggingLevel);
                    _unitOfWork.SettingsRepository.Update(setting);
                }
            }
            
            _configuration.GetSection("Logging:LogLevel:Default").Value = updateSettingsDto.LoggingLevel + "";
            if (!_unitOfWork.HasChanges()) return Ok("Nothing was updated");

            if (!_unitOfWork.HasChanges() || !await _unitOfWork.Complete())
                return BadRequest("There was a critical issue. Please try again.");
            
            _logger.LogInformation("Server Settings updated");
            _taskScheduler.ScheduleTasks();
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
            return Ok(Enum.GetNames(typeof(LibraryType)));
        }
        
        [Authorize(Policy = "RequireAdminRole")]
        [HttpGet("log-levels")]
        public ActionResult<IEnumerable<string>> GetLogLevels()
        {
            return Ok(new [] {"Trace", "Debug", "Information", "Warning", "Critical"});
        }
    }
}