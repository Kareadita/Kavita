using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
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
using EasyCaching.Core;
using Hangfire;
using Hangfire.Storage;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MimeTypes;
using TaskScheduler = API.Services.TaskScheduler;

namespace API.Controllers;

#nullable enable

[Authorize(Policy = "RequireAdminRole")]
public class ServerController : BaseApiController
{
    private readonly ILogger<ServerController> _logger;
    private readonly IBackupService _backupService;
    private readonly IArchiveService _archiveService;
    private readonly IVersionUpdaterService _versionUpdaterService;
    private readonly IStatsService _statsService;
    private readonly ICleanupService _cleanupService;
    private readonly IScannerService _scannerService;
    private readonly IAccountService _accountService;
    private readonly ITaskScheduler _taskScheduler;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEasyCachingProviderFactory _cachingProviderFactory;
    private readonly ILocalizationService _localizationService;
    private readonly IEmailService _emailService;

    public ServerController(ILogger<ServerController> logger,
        IBackupService backupService, IArchiveService archiveService, IVersionUpdaterService versionUpdaterService, IStatsService statsService,
        ICleanupService cleanupService, IScannerService scannerService, IAccountService accountService,
        ITaskScheduler taskScheduler, IUnitOfWork unitOfWork, IEasyCachingProviderFactory cachingProviderFactory,
        ILocalizationService localizationService, IEmailService emailService)
    {
        _logger = logger;
        _backupService = backupService;
        _archiveService = archiveService;
        _versionUpdaterService = versionUpdaterService;
        _statsService = statsService;
        _cleanupService = cleanupService;
        _scannerService = scannerService;
        _accountService = accountService;
        _taskScheduler = taskScheduler;
        _unitOfWork = unitOfWork;
        _cachingProviderFactory = cachingProviderFactory;
        _localizationService = localizationService;
        _emailService = emailService;
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
    public async Task<ActionResult> AnalyzeFiles()
    {
        _logger.LogInformation("{UserName} is performing file analysis from admin dashboard", User.GetUsername());
        if (TaskScheduler.HasAlreadyEnqueuedTask(ScannerService.Name, "AnalyzeFiles",
                Array.Empty<object>(), TaskScheduler.DefaultQueue, true))
            return Ok(await _localizationService.Translate(User.GetUserId(), "job-already-running"));

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
    /// Returns non-sensitive information about the current system
    /// </summary>
    /// <remarks>This is just for the UI and is extremely lightweight</remarks>
    /// <returns></returns>
    [HttpGet("server-info-slim")]
    public async Task<ActionResult<ServerInfoDto>> GetSlimVersion()
    {
        return Ok(await _statsService.GetServerInfoSlim());
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
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "encode-as-warning"));
        }

        _taskScheduler.CovertAllCoversToEncoding();

        return Ok();
    }

    /// <summary>
    /// Downloads all the log files via a zip
    /// </summary>
    /// <returns></returns>
    [HttpGet("logs")]
    public async Task<ActionResult> GetLogs()
    {
        var files = _backupService.GetLogFiles();
        try
        {
            var zipPath =  _archiveService.CreateZipForDownload(files, "logs");
            return PhysicalFile(zipPath, MimeTypeMap.GetMimeType(Path.GetExtension(zipPath)),
                System.Web.HttpUtility.UrlEncode(Path.GetFileName(zipPath)), true);
        }
        catch (KavitaException ex)
        {
            return BadRequest(await _localizationService.Translate(User.GetUserId(), ex.Message));
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
    /// Returns how many versions out of date this install is
    /// </summary>
    [HttpGet("check-out-of-date")]
    public async Task<ActionResult<int>> CheckHowOutOfDate()
    {
        return Ok(await _versionUpdaterService.GetNumberOfReleasesBehind());
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
                    LastExecutionUtc = dto.LastExecution.HasValue ? new DateTime(dto.LastExecution.Value.Ticks, DateTimeKind.Utc) : null
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


    /// <summary>
    /// Bust Kavita+ Cache
    /// </summary>
    /// <returns></returns>
    [Authorize("RequireAdminRole")]
    [HttpPost("bust-kavitaplus-cache")]
    public async Task<ActionResult> BustReviewAndRecCache()
    {
        _logger.LogInformation("Busting Kavita+ Cache");
        var provider = _cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.KavitaPlusExternalSeries);
        await provider.FlushAsync();
        provider = _cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.KavitaPlusSeriesDetail);
        await provider.FlushAsync();
        return Ok();
    }

    /// <summary>
    /// Checks for updates and pushes an event to the UI
    /// </summary>
    /// <returns></returns>
    [HttpGet("check-for-updates")]
    public async Task<ActionResult> CheckForAnnouncements()
    {
        await _taskScheduler.CheckForUpdate();
        return Ok();
    }
}
