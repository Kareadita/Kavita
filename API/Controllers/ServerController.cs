using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Jobs;
using API.DTOs.Stats;
using API.DTOs.Update;
using API.Extensions;
using API.Logging;
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
using TaskScheduler = API.Services.TaskScheduler;

namespace API.Controllers;

[Authorize(Policy = "RequireAdminRole")]
public class ServerController : BaseApiController
{
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ILogger<ServerController> _logger;
    private readonly IBackupService _backupService;
    private readonly IArchiveService _archiveService;
    private readonly IVersionUpdaterService _versionUpdaterService;
    private readonly IStatsService _statsService;
    private readonly ICleanupService _cleanupService;
    private readonly IEmailService _emailService;
    private readonly IBookmarkService _bookmarkService;

    public ServerController(IHostApplicationLifetime applicationLifetime, ILogger<ServerController> logger,
        IBackupService backupService, IArchiveService archiveService, IVersionUpdaterService versionUpdaterService, IStatsService statsService,
        ICleanupService cleanupService, IEmailService emailService, IBookmarkService bookmarkService)
    {
        _applicationLifetime = applicationLifetime;
        _logger = logger;
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
        _cleanupService.CleanupCacheAndTempDirectories();

        return Ok();
    }

    /// <summary>
    /// Performs an ad-hoc cleanup of Want To Read, by removing want to read series for users, where the series are fully read and in Completed publication status.
    /// </summary>
    /// <returns></returns>
    [HttpPost("cleanup-want-to-read")]
    public ActionResult CleanupWantToRead()
    {
        _logger.LogInformation("{UserName} is clearing running want to read cleanup from admin dashboard", User.GetUsername());
        RecurringJob.TriggerJob(API.Services.TaskScheduler.RemoveFromWantToReadTaskId);

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
        RecurringJob.TriggerJob(API.Services.TaskScheduler.BackupTaskId);
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
        if (TaskScheduler.HasAlreadyEnqueuedTask(BookmarkService.Name, "ConvertAllBookmarkToWebP", Array.Empty<object>(),
                TaskScheduler.DefaultQueue, true)) return Ok();
        BackgroundJob.Enqueue(() => _bookmarkService.ConvertAllBookmarkToWebP());
        return Ok();
    }

    [HttpGet("logs")]
    public ActionResult GetLogs()
    {
        var files = _backupService.GetLogFiles();
        try
        {
            var zipPath =  _archiveService.CreateZipForDownload(files, "logs");
            return PhysicalFile(zipPath, "application/zip", Path.GetFileName(zipPath), true);
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
        var recurringJobs = JobStorage.Current.GetConnection().GetRecurringJobs().Select(
            dto =>
                new JobDto() {
                    Id = dto.Id,
                    Title = dto.Id.Replace('-', ' '),
                    Cron = dto.Cron,
                    CreatedAt = dto.CreatedAt,
                    LastExecution = dto.LastExecution,
                });

        return Ok(recurringJobs);

    }
}
