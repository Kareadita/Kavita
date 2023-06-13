using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Jobs;
using API.DTOs.MediaErrors;
using API.DTOs.Stats;
using API.DTOs.Update;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using API.Services;
using API.Services.Tasks;
using Hangfire;
using Hangfire.Storage;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TaskScheduler = API.Services.TaskScheduler;

namespace API.Controllers;

[Authorize(Policy = "RequireAdminRole")]
public class ServerController : BaseApiController
{
    private readonly ILogger<ServerController> _logger;
    private readonly IBackupService _backupService;
    private readonly IArchiveService _archiveService;
    private readonly IVersionUpdaterService _versionUpdaterService;
    private readonly IStatsService _statsService;
    private readonly ICleanupService _cleanupService;
    private readonly IBookmarkService _bookmarkService;
    private readonly IScannerService _scannerService;
    private readonly IAccountService _accountService;
    private readonly ITaskScheduler _taskScheduler;
    private readonly IUnitOfWork _unitOfWork;

    public ServerController(ILogger<ServerController> logger,
        IBackupService backupService, IArchiveService archiveService, IVersionUpdaterService versionUpdaterService, IStatsService statsService,
        ICleanupService cleanupService, IBookmarkService bookmarkService, IScannerService scannerService, IAccountService accountService,
        ITaskScheduler taskScheduler, IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _backupService = backupService;
        _archiveService = archiveService;
        _versionUpdaterService = versionUpdaterService;
        _statsService = statsService;
        _cleanupService = cleanupService;
        _bookmarkService = bookmarkService;
        _scannerService = scannerService;
        _accountService = accountService;
        _taskScheduler = taskScheduler;
        _unitOfWork = unitOfWork;
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
        RecurringJob.TriggerJob(TaskScheduler.RemoveFromWantToReadTaskId);

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
        RecurringJob.TriggerJob(TaskScheduler.BackupTaskId);
        return Ok();
    }

    /// <summary>
    /// This is a one time task that needs to be ran for v0.7 statistics to work
    /// </summary>
    /// <returns></returns>
    [HttpPost("analyze-files")]
    public ActionResult AnalyzeFiles()
    {
        _logger.LogInformation("{UserName} is performing file analysis from admin dashboard", User.GetUsername());
        if (TaskScheduler.HasAlreadyEnqueuedTask(ScannerService.Name, "AnalyzeFiles",
                Array.Empty<object>(), TaskScheduler.DefaultQueue, true))
            return Ok("Job already running");

        BackgroundJob.Enqueue(() => _scannerService.AnalyzeFiles());
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
    /// Triggers the scheduling of the convert media job. This will convert all media to the target encoding (except for PNG). Only one job will run at a time.
    /// </summary>
    /// <returns></returns>
    [HttpPost("convert-media")]
    public async Task<ActionResult> ScheduleConvertCovers()
    {
        var encoding = (await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EncodeMediaAs;
        if (encoding == EncodeFormat.PNG)
        {
            return BadRequest(
                "You cannot convert to PNG. For covers, use Refresh Covers. Bookmarks and favicons cannot be encoded back.");
        }
        BackgroundJob.Enqueue(() => _taskScheduler.CovertAllCoversToEncoding());

        return Ok();
    }

    /// <summary>
    /// Downloads all the log files via a zip
    /// </summary>
    /// <returns></returns>
    [HttpGet("logs")]
    public ActionResult GetLogs()
    {
        var files = _backupService.GetLogFiles();
        try
        {
            var zipPath =  _archiveService.CreateZipForDownload(files, "logs");
            return PhysicalFile(zipPath, "application/zip",
                System.Web.HttpUtility.UrlEncode(Path.GetFileName(zipPath)), true);
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


    /// <summary>
    /// Checks for updates, if no updates that are > current version installed, returns null
    /// </summary>
    [HttpPost("scrobble-updates")]
    public async Task<ActionResult> TriggerScrobbleUpdates()
    {
        await _taskScheduler.ScrobbleUpdates(User.GetUserId());
        return Ok();
    }

    /// <summary>
    /// Pull the Changelog for Kavita from Github and display
    /// </summary>
    /// <returns></returns>
    [HttpGet("changelog")]
    public async Task<ActionResult<IEnumerable<UpdateNotificationDto>>> GetChangelog()
    {
        return Ok(await _versionUpdaterService.GetAllReleases());
    }

    /// <summary>
    /// Is this server accessible to the outside net
    /// </summary>
    /// <remarks>If the instance has the HostName set, this will return true whether or not it is accessible externally</remarks>
    /// <returns></returns>
    [HttpGet("accessible")]
    [AllowAnonymous]
    public async Task<ActionResult<bool>> IsServerAccessible()
    {
        return Ok(await _accountService.CheckIfAccessible(Request));
    }

    /// <summary>
    /// Returns a list of reoccurring jobs. Scheduled ad-hoc jobs will not be returned.
    /// </summary>
    /// <returns></returns>
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

    /// <summary>
    /// Returns a list of issues found during scanning or reading in which files may have corruption or bad metadata (structural metadata)
    /// </summary>
    /// <returns></returns>
    [Authorize("RequireAdminRole")]
    [HttpGet("media-errors")]
    public ActionResult<PagedList<MediaErrorDto>> GetMediaErrors()
    {
        return Ok(_unitOfWork.MediaErrorRepository.GetAllErrorDtosAsync());
    }

    /// <summary>
    /// Deletes all media errors
    /// </summary>
    /// <returns></returns>
    [Authorize("RequireAdminRole")]
    [HttpPost("clear-media-alerts")]
    public async Task<ActionResult> ClearMediaErrors()
    {
        await _unitOfWork.MediaErrorRepository.DeleteAll();
        return Ok();
    }


}
