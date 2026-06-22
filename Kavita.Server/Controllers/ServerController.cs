using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EasyCaching.Core;
using Hangfire;
using Hangfire.Storage;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.Scanner;
using Kavita.Common;
using Kavita.Common.Helpers;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.Jobs;
using Kavita.Models.DTOs.MediaErrors;
using Kavita.Models.DTOs.Stats;
using Kavita.Models.DTOs.Update;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Scanner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MimeTypes;
using TaskScheduler = Kavita.Services.TaskScheduler;

namespace Kavita.Server.Controllers;

[Authorize(PolicyGroups.AdminPolicy)]
public class ServerController(
    ILogger<ServerController> logger,
    IBackupService backupService,
    IArchiveService archiveService,
    IVersionUpdaterService versionUpdaterService,
    IStatsService statsService,
    ICleanupService cleanupService,
    IScannerService scannerService,
    ITaskScheduler taskScheduler,
    IUnitOfWork unitOfWork,
    IEasyCachingProviderFactory cachingProviderFactory,
    IThemeService themeService,
    ILocalizationService localizationService)
    : BaseApiController
{
    /// <summary>
    /// Performs an ad-hoc cleanup of Cache
    /// </summary>
    /// <returns></returns>
    [HttpPost("clear-cache")]
    public ActionResult ClearCache()
    {
        logger.LogInformation("{UserName} is clearing cache of server from admin dashboard", Username!);
        cleanupService.CleanupCacheAndTempDirectories();

        return Ok();
    }

    /// <summary>
    /// Performs an ad-hoc cleanup of Want To Read, by removing want to read series for users, where the series are fully read and in Completed publication status.
    /// </summary>
    /// <returns></returns>
    [HttpPost("cleanup-want-to-read")]
    public ActionResult CleanupWantToRead()
    {
        logger.LogInformation("{UserName} is clearing running want to read cleanup from admin dashboard", Username!);
        RecurringJob.TriggerJob(TaskScheduler.RemoveFromWantToReadTaskId);

        return Ok();
    }

    /// <summary>
    /// Performs the nightly maintenance work on the Server. Can be heavy.
    /// </summary>
    /// <returns></returns>
    [HttpPost("cleanup")]
    public ActionResult Cleanup()
    {
        logger.LogInformation("{UserName} is clearing running general cleanup from admin dashboard", Username!);
        RecurringJob.TriggerJob(TaskScheduler.CleanupTaskId);

        return Ok();
    }

    /// <summary>
    /// Performs an ad-hoc backup of the Database
    /// </summary>
    /// <returns></returns>
    [HttpPost("backup-db")]
    public ActionResult BackupDatabase()
    {
        logger.LogInformation("{UserName} is backing up database of server from admin dashboard", Username!);
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
        logger.LogInformation("{UserName} is performing file analysis from admin dashboard", Username!);
        if (TaskScheduler.HasAlreadyEnqueuedTask(ScannerService.Name, "AnalyzeFiles",
                [], TaskScheduler.DefaultQueue, true))
            return Ok(await localizationService.TranslateAsync(UserId, "job-already-running"));

        BackgroundJob.Enqueue(() => scannerService.AnalyzeFiles());
        return Ok();
    }


    /// <summary>
    /// Returns non-sensitive information about the current system
    /// </summary>
    /// <remarks>This is just for the UI and is extremely lightweight</remarks>
    /// <returns></returns>
    [HttpGet("server-info-slim")]
    public async Task<ActionResult<ServerInfoSlimDto>> GetSlimVersion()
    {
        return Ok(await statsService.GetServerInfoSlim());
    }


    /// <summary>
    /// Triggers the scheduling of the convert media job. This will convert all media to the target encoding (except for PNG). Only one job will run at a time.
    /// </summary>
    /// <returns></returns>
    [HttpPost("convert-media")]
    public async Task<ActionResult> ScheduleConvertCovers()
    {
        var encoding = (await unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EncodeMediaAs;
        if (encoding == EncodeFormat.PNG)
        {
            return BadRequest(await localizationService.TranslateAsync(UserId, "encode-as-warning"));
        }

        taskScheduler.ConvertAllCoversToEncoding();

        return Ok();
    }

    /// <summary>
    /// Downloads all the log files via a zip
    /// </summary>
    /// <returns></returns>
    [HttpGet("logs")]
    public async Task<ActionResult> GetLogs()
    {
        var files = backupService.GetLogFiles();
        try
        {
            var zipPath = archiveService.CreateZipForDownload(files, "logs");
            return PhysicalFile(zipPath, MimeTypeMap.GetMimeType(Path.GetExtension(zipPath)),
                System.Web.HttpUtility.UrlEncode(Path.GetFileName(zipPath)), true);
        }
        catch (KavitaException ex)
        {
            return BadRequest(await localizationService.TranslateAsync(UserId, ex.Message));
        }
    }

    /// <summary>
    /// Checks for updates and pushes an event to the UI
    /// </summary>
    /// <remarks>Some users have websocket issues so this is not always reliable to alert the user</remarks>
    [HttpGet("check-for-updates")]
    public async Task<ActionResult> CheckForAnnouncements()
    {
        await taskScheduler.CheckForUpdate();
        return Ok();
    }

    /// <summary>
    /// Checks for updates, if no updates that are > current version installed, returns null
    /// </summary>
    [HttpGet("check-update")]
    public async Task<ActionResult<UpdateNotificationDto?>> CheckForUpdates()
    {
        return Ok(await versionUpdaterService.CheckForUpdate());
    }

    /// <summary>
    /// Returns how many versions out of date this install is
    /// </summary>
    /// <param name="stableOnly">Only count Stable releases</param>
    [HttpGet("check-out-of-date")]
    public async Task<ActionResult<int>> CheckHowOutOfDate(bool stableOnly = true)
    {
        return Ok(await versionUpdaterService.GetNumberOfReleasesBehind(stableOnly));
    }


    /// <summary>
    /// Pull the Changelog for Kavita from Github and display
    /// </summary>
    /// <param name="count">How many releases from the latest to return</param>
    /// <returns></returns>
    [HttpGet("changelog")]
    public async Task<ActionResult<IEnumerable<UpdateNotificationDto>>> GetChangelog(int count = 0)
    {
        return Ok(await versionUpdaterService.GetAllReleases(count));
    }

    /// <summary>
    /// Returns a list of reoccurring jobs. Scheduled ad-hoc jobs will not be returned.
    /// </summary>
    /// <returns></returns>
    [HttpGet("jobs")]
    public async Task<ActionResult<IEnumerable<JobDto>>> GetJobs()
    {
        var jobDtoTasks = JobStorage.Current.GetConnection().GetRecurringJobs().Select(async dto =>
            new JobDto()
            {
                Id = dto.Id,
                Title = await localizationService.TranslateAsync(UserId, dto.Id),
                Cron = dto.Cron,
                LastExecutionUtc = dto.LastExecution.HasValue ? new DateTime(dto.LastExecution.Value.Ticks, DateTimeKind.Utc) : null
            });

        return Ok(await Task.WhenAll(jobDtoTasks));
    }

    /// <summary>
    /// Returns a list of issues found during scanning or reading in which files may have corruption or bad metadata (structural metadata)
    /// </summary>
    /// <returns></returns>
    [Authorize(PolicyGroups.AdminPolicy)]
    [HttpGet("media-errors")]
    public async Task<ActionResult<IList<MediaErrorDto>>> GetMediaErrors()
    {
        return Ok(await unitOfWork.MediaErrorRepository.GetAllErrorDtosAsync());
    }

    /// <summary>
    /// Deletes all media errors
    /// </summary>
    /// <returns></returns>
    [Authorize(PolicyGroups.AdminPolicy)]
    [HttpPost("clear-media-alerts")]
    public async Task<ActionResult> ClearMediaErrors()
    {
        await unitOfWork.MediaErrorRepository.DeleteAll();
        return Ok();
    }


    /// <summary>
    /// Bust Kavita+ Cache
    /// </summary>
    /// <returns></returns>
    [Authorize(PolicyGroups.AdminPolicy)]
    [HttpPost("bust-kavitaplus-cache")]
    public async Task<ActionResult> BustReviewAndRecCache()
    {
        logger.LogInformation("Busting Kavita+ Cache");
        var provider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.KavitaPlusExternalSeries);
        await provider.FlushAsync();
        return Ok();
    }

    /// <summary>
    /// Runs the Sync Themes task
    /// </summary>
    /// <returns></returns>
    [Authorize(PolicyGroups.AdminPolicy)]
    [HttpPost("sync-themes")]
    public async Task<ActionResult> SyncThemes()
    {
        await themeService.SyncThemes();
        return Ok();
    }

}
