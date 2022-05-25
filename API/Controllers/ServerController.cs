﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Jobs;
using API.DTOs.Stats;
using API.DTOs.Update;
using API.Extensions;
using API.Services;
using API.Services.Tasks;
using Hangfire;
using Hangfire.Storage;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaskScheduler = System.Threading.Tasks.TaskScheduler;

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
        private readonly IVersionUpdaterService _versionUpdaterService;
        private readonly IStatsService _statsService;
        private readonly ICleanupService _cleanupService;
        private readonly IEmailService _emailService;
        private readonly IBookmarkService _bookmarkService;

        public ServerController(IHostApplicationLifetime applicationLifetime, ILogger<ServerController> logger, IConfiguration config,
            IBackupService backupService, IArchiveService archiveService, IVersionUpdaterService versionUpdaterService, IStatsService statsService,
            ICleanupService cleanupService, IEmailService emailService, IBookmarkService bookmarkService)
        {
            _applicationLifetime = applicationLifetime;
            _logger = logger;
            _config = config;
            _backupService = backupService;
            _archiveService = archiveService;
            _versionUpdaterService = versionUpdaterService;
            _statsService = statsService;
            _cleanupService = cleanupService;
            _emailService = emailService;
            _bookmarkService = bookmarkService;
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
            _cleanupService.CleanupCacheDirectory();

            return Ok();
        }

        /// <summary>
        /// Performs an ad-hoc backup of the Database
        /// </summary>
        /// <returns></returns>
        [HttpPost("backup-db")]
        public ActionResult BackupDatabase()
        {
            _logger.LogInformation("{UserName} is backing up database of server from admin dashboard", User.GetUsername());
            RecurringJob.Trigger("backup");
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

        /// <summary>
        /// Triggers the scheduling of the convert bookmarks job. Only one job will run at a time.
        /// </summary>
        /// <returns></returns>
        [HttpPost("convert-bookmarks")]
        public ActionResult ScheduleConvertBookmarks()
        {
            BackgroundJob.Enqueue(() => _bookmarkService.ConvertAllBookmarkToWebP());
            return Ok();
        }

        [HttpGet("logs")]
        public async Task<ActionResult> GetLogs()
        {
            var files = _backupService.GetLogFiles(_config.GetMaxRollingFiles(), _config.GetLoggingFileName());
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

        /// <summary>
        /// Checks for updates, if no updates that are > current version installed, returns null
        /// </summary>
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

        /// <summary>
        /// Is this server accessible to the outside net
        /// </summary>
        /// <returns></returns>
        [HttpGet("accessible")]
        [AllowAnonymous]
        public async Task<ActionResult<bool>> IsServerAccessible()
        {
            return await _emailService.CheckIfAccessible(Request.Host.ToString());
        }

        [HttpGet("jobs")]
        public ActionResult<IEnumerable<JobDto>> GetJobs()
        {
            var recurringJobs = Hangfire.JobStorage.Current.GetConnection().GetRecurringJobs().Select(
                dto =>
                new JobDto() {
                    Id = dto.Id,
                    Title = dto.Id.Replace('-', ' '),
                    Cron = dto.Cron,
                    CreatedAt = dto.CreatedAt,
                    LastExecution = dto.LastExecution,
                });

            // For now, let's just do something simple
            //var enqueuedJobs =  JobStorage.Current.GetMonitoringApi().EnqueuedJobs("default", 0, int.MaxValue);
            return Ok(recurringJobs);

        }
    }
}
