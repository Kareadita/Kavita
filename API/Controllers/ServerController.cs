using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using API.DTOs.Stats;
using API.DTOs.Update;
using API.Extensions;
using API.Interfaces.Services;
using API.Services.Tasks;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace API.Controllers
{
    [Authorize(Policy = "RequireAdminRole")]
    public class ServerController : BaseApiController
    {
        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly ILogger<ServerController> _logger;
        private readonly IConfiguration _config;
        private readonly IBackupService _backupService;
        private readonly IArchiveService _archiveService;
        private readonly ICacheService _cacheService;
        private readonly IVersionUpdaterService _versionUpdaterService;
        private readonly IStatsService _statsService;

        public ServerController(IHostApplicationLifetime applicationLifetime, ILogger<ServerController> logger, IConfiguration config,
            IBackupService backupService, IArchiveService archiveService, ICacheService cacheService,
            IVersionUpdaterService versionUpdaterService, IStatsService statsService)
        {
            _applicationLifetime = applicationLifetime;
            _logger = logger;
            _config = config;
            _backupService = backupService;
            _archiveService = archiveService;
            _cacheService = cacheService;
            _versionUpdaterService = versionUpdaterService;
            _statsService = statsService;
        }

        /// <summary>
        /// Attempts to Restart the server. Does not work, will shutdown the instance.
        /// </summary>
        /// <returns></returns>
        [HttpPost("restart")]
        public ActionResult RestartServer()
        {
            _logger.LogInformation("{UserName} is restarting server from admin dashboard", User.GetUsername());

            _applicationLifetime.StopApplication();
            return Ok();
        }

        /// <summary>
        /// Performs an ad-hoc cleanup of Cache
        /// </summary>
        /// <returns></returns>
        [HttpPost("clear-cache")]
        public ActionResult ClearCache()
        {
            _logger.LogInformation("{UserName} is clearing cache of server from admin dashboard", User.GetUsername());
            _cacheService.Cleanup();

            return Ok();
        }

        /// <summary>
        /// Performs an ad-hoc backup of the Database
        /// </summary>
        /// <returns></returns>
        [HttpPost("backup-db")]
        public async Task<ActionResult> BackupDatabase()
        {
            _logger.LogInformation("{UserName} is backing up database of server from admin dashboard", User.GetUsername());
            await _backupService.BackupDatabase();

            return Ok();
        }

        /// <summary>
        /// Returns non-sensitive information about the current system
        /// </summary>
        /// <returns></returns>
        [HttpGet("server-info")]
        public async Task<ActionResult<ServerInfoDto>> GetVersion()
        {
           return Ok(await _statsService.GetServerInfo());
        }

        [HttpGet("logs")]
        public async Task<ActionResult> GetLogs()
        {
            var files = _backupService.LogFiles(_config.GetMaxRollingFiles(), _config.GetLoggingFileName());
            try
            {
                var (fileBytes, zipPath) = await _archiveService.CreateZipForDownload(files, "logs");
                return File(fileBytes, "application/zip", Path.GetFileName(zipPath));
            }
            catch (KavitaException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("check-update")]
        public async Task<ActionResult<UpdateNotificationDto>> CheckForUpdates()
        {
            return Ok(await _versionUpdaterService.CheckForUpdate());
        }

        [HttpGet("changelog")]
        public async Task<ActionResult<IEnumerable<UpdateNotificationDto>>> GetChangelog()
        {
            return Ok(await _versionUpdaterService.GetAllReleases());
        }
    }
}
