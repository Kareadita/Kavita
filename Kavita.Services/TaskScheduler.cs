using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.Metadata;
using Kavita.API.Services.Plus;
using Kavita.API.Services.Reading;
using Kavita.API.Services.ReadingLists;
using Kavita.API.Services.Scanner;
using Kavita.API.Services.SignalR;
using Kavita.Common.Constants;
using Kavita.Common.Extensions;
using Kavita.Common.Helpers;
using Kavita.Models.Constants;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.User;
using Kavita.Models.Extensions;
using Kavita.Models.Scanner;
using Kavita.Services.Extensions;
using Kavita.Services.Plus;
using Kavita.Services.ReadingLists;
using Kavita.Services.Scanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace Kavita.Services;

public class TaskScheduler : ITaskScheduler
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<TaskScheduler> _logger;
    private readonly IScannerService _scannerService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMetadataService _metadataService;
    private readonly IBackupService _backupService;
    private readonly ICleanupService _cleanupService;

    private readonly IStatsService _statsService;
    private readonly IVersionUpdaterService _versionUpdaterService;
    private readonly IWordCountAnalyzerService _wordCountAnalyzerService;
    private readonly IMediaConversionService _mediaConversionService;
    private readonly IScrobblingService _scrobblingService;
    private readonly ILicenseService _licenseService;
    private readonly IExternalMetadataService _externalMetadataService;
    private readonly ISmartCollectionSyncService _smartCollectionSyncService;
    private readonly IWantToReadSyncService _wantToReadSyncService;
    private readonly IEventHub _eventHub;
    private readonly IEmailService _emailService;
    private readonly IAuthKeyService _authKeyService;

    public static BackgroundJobServer Client => new ();
    public const string ScanQueue = TaskSchedulerConstants.ScanQueue;
    public const string DefaultQueue = TaskSchedulerConstants.DefaultQueue;
    public const string RemoveFromWantToReadTaskId = TaskSchedulerConstants.RemoveFromWantToReadTaskId;
    public const string UpdateYearlyStatsTaskId = TaskSchedulerConstants.UpdateYearlyStatsTaskId;
    public const string SyncThemesTaskId = TaskSchedulerConstants.SyncThemesTaskId;
    public const string CheckForUpdateId = TaskSchedulerConstants.CheckForUpdateId;
    public const string CleanupDbTaskId = TaskSchedulerConstants.CleanupDbTaskId;
    public const string CleanupTaskId = TaskSchedulerConstants.CleanupTaskId;
    public const string TaskCblSyncId = TaskSchedulerConstants.TaskCblSyncId;
    public const string BackupTaskId = TaskSchedulerConstants.BackupTaskId;
    public const string ScanLibrariesTaskId = TaskSchedulerConstants.ScanLibrariesTaskId;
    public const string ReportStatsTaskId = TaskSchedulerConstants.ReportStatsTaskId;
    public const string CheckScrobblingTokensId = TaskSchedulerConstants.CheckScrobblingTokensId;
    public const string ProcessScrobblingEventsId = TaskSchedulerConstants.ProcessScrobblingEventsId;
    public const string ProcessProcessedScrobblingEventsId = TaskSchedulerConstants.ProcessProcessedScrobblingEventsId;
    public const string LicenseCheckId = TaskSchedulerConstants.LicenseCheckId;
    public const string KavitaPlusDataRefreshId = TaskSchedulerConstants.KavitaPlusDataRefreshId;
    public const string KavitaPlusStackSyncId = TaskSchedulerConstants.KavitaPlusStackSyncId;
    public const string KavitaPlusWantToReadSyncId = TaskSchedulerConstants.KavitaPlusWantToReadSyncId;
    public const string ReadingHistoryAggregationId = TaskSchedulerConstants.ReadingHistoryAggregationId;
    public const string AuthKeyExpirationId = TaskSchedulerConstants.AuthKeyExpirationId;
    public const string EnsureSideNavId = TaskSchedulerConstants.EnsureSideNavId;
    public const string FlushUserActiveTaskId = TaskSchedulerConstants.FlushUserActiveTaskId;
    public const string PurgeKavitaPlusAuditLogsId = TaskSchedulerConstants.PurgeKavitaPlusAuditLogsId;

    private const int BaseRetryDelay = 60; // 1-minute

    private static readonly ImmutableArray<string> NonCronOptions = ["disabled", "daily", "weekly"];
    /// <summary>
    /// Held while deciding whether a scan is already queued and queueing one, so two requests cannot both see an empty queue
    /// </summary>
    internal static readonly Lock ScanRequestLock = new();

    private static readonly Random Rnd = new Random();
    private const int ScanHourDelay = 3;
    /// <summary>
    /// A watcher scan runs once its folder has had no changes for this long
    /// </summary>
    internal static readonly TimeSpan WatcherQuietTime = TimeSpan.FromSeconds(30);
    /// <summary>
    /// A watcher scan runs by this long after its job was created, even while changes keep coming
    /// </summary>
    internal static readonly TimeSpan WatcherScanCap = TimeSpan.FromMinutes(10);

    private static readonly RecurringJobOptions RecurringJobOptions = new RecurringJobOptions()
    {
        TimeZone = TimeZoneInfo.Local
    };
    /// <summary>
    /// Retry policy, with 3 tries, and a 1-minute base delay
    /// </summary>
    private readonly AsyncRetryPolicy _defaultRetryPolicy;


    public TaskScheduler(ICacheService cacheService, ILogger<TaskScheduler> logger, IScannerService scannerService,
        IUnitOfWork unitOfWork, IMetadataService metadataService, IBackupService backupService,
        ICleanupService cleanupService, IStatsService statsService, IVersionUpdaterService versionUpdaterService,
        IWordCountAnalyzerService wordCountAnalyzerService,
        IMediaConversionService mediaConversionService, IScrobblingService scrobblingService, ILicenseService licenseService,
        IExternalMetadataService externalMetadataService, ISmartCollectionSyncService smartCollectionSyncService,
        IWantToReadSyncService wantToReadSyncService, IEventHub eventHub, IEmailService emailService,
        IAuthKeyService authKeyService)
    {
        _cacheService = cacheService;
        _logger = logger;
        _scannerService = scannerService;
        _unitOfWork = unitOfWork;
        _metadataService = metadataService;
        _backupService = backupService;
        _cleanupService = cleanupService;
        _statsService = statsService;
        _versionUpdaterService = versionUpdaterService;
        _wordCountAnalyzerService = wordCountAnalyzerService;
        _mediaConversionService = mediaConversionService;
        _scrobblingService = scrobblingService;
        _licenseService = licenseService;
        _externalMetadataService = externalMetadataService;
        _smartCollectionSyncService = smartCollectionSyncService;
        _wantToReadSyncService = wantToReadSyncService;
        _eventHub = eventHub;
        _emailService = emailService;
        _authKeyService = authKeyService;

        _defaultRetryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt =>
                {
                    var delay = BaseRetryDelay * 2 * attempt;
                    var jitter = Random.Shared.Next(0, delay / 4);

                    return TimeSpan.FromSeconds(delay + jitter);
                },
                onRetry: (ex, timeSpan, attempt) =>
                {
                    _logger.LogWarning(ex, "Attempt {Attempt} failed, retrying in {Delay}ms",
                        attempt, timeSpan.TotalMilliseconds);
                }
            );
    }

    public async Task ScheduleTasks(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Scheduling reoccurring tasks");


        var setting = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.TaskScan, cancellationToken)).Value;
        if (IsInvalidCronSetting(setting))
        {
            _logger.LogError("Scan Task has invalid cron, defaulting to Daily");
            RecurringJob.AddOrUpdate(ScanLibrariesTaskId, () => EnqueueScanLibraries(false),
                Cron.Daily, RecurringJobOptions);
        }
        else
        {
            var scanLibrarySetting = setting;
            _logger.LogDebug("Scheduling Scan Library Task for {Setting}", scanLibrarySetting);
            RecurringJob.AddOrUpdate(ScanLibrariesTaskId, () => EnqueueScanLibraries(false),
                () => CronConverter.ConvertToCronNotation(scanLibrarySetting), RecurringJobOptions);
        }


        setting = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.TaskBackup, cancellationToken)).Value;
        if (IsInvalidCronSetting(setting))
        {
            _logger.LogError("Backup Task has invalid cron, defaulting to Weekly");
            RecurringJob.AddOrUpdate<IBackupService>(BackupTaskId, (service) => service.BackupDatabase(CancellationToken.None),
                Cron.Weekly, RecurringJobOptions);
        }
        else
        {
            _logger.LogDebug("Scheduling Backup Task for {Setting}", setting);
            var schedule = CronConverter.ConvertToCronNotation(setting);
            if (schedule == Cron.Daily())
            {
                // Override daily and make 2am so that everything on system has cleaned up and no blocking
                schedule = Cron.Daily(2);
            }
            RecurringJob.AddOrUpdate<IBackupService>(BackupTaskId, (service) => service.BackupDatabase(CancellationToken.None),
                () => schedule, RecurringJobOptions);
        }

        setting = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.TaskCleanup, cancellationToken)).Value;
        if (IsInvalidCronSetting(setting))
        {
            _logger.LogError("Cleanup Task has invalid cron, defaulting to Daily");
            RecurringJob.AddOrUpdate<ICleanupService>(CleanupTaskId, (service) => service.Cleanup(CancellationToken.None),
                Cron.Daily, RecurringJobOptions);
        }
        else
        {
            _logger.LogDebug("Scheduling Cleanup Task for {Setting}", setting);
            RecurringJob.AddOrUpdate<ICleanupService>(CleanupTaskId, (service) => service.Cleanup(CancellationToken.None),
                CronConverter.ConvertToCronNotation(setting), RecurringJobOptions);
        }

        setting = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.TaskCblSync, cancellationToken)).Value;
        if (IsInvalidCronSetting(setting))
        {
            _logger.LogError("CBL Sync Task has invalid cron, defaulting to Daily");
            RecurringJob.AddOrUpdate<ICblImportService>(TaskCblSyncId, service => service.SyncAllReadingLists(CancellationToken.None),
                "0 4 * * *", RecurringJobOptions);
        }
        else
        {
            _logger.LogDebug("Scheduling CBL Sync Task for {Setting}", setting);
            RecurringJob.AddOrUpdate<ICblImportService>(TaskCblSyncId, service => service.SyncAllReadingLists(CancellationToken.None),
                CronConverter.ConvertToCronNotation(setting), RecurringJobOptions);
        }


        RecurringJob.AddOrUpdate<ICleanupService>(RemoveFromWantToReadTaskId,
            (service) => service.CleanupWantToRead(CancellationToken.None),
            Cron.Daily, RecurringJobOptions);
        RecurringJob.AddOrUpdate<IStatisticService>(UpdateYearlyStatsTaskId,
            (service) => service.UpdateServerStatistics(CancellationToken.None),
            Cron.Monthly, RecurringJobOptions);

        RecurringJob.AddOrUpdate<IThemeService>(SyncThemesTaskId,
            themeService => themeService.SyncThemes(CancellationToken.None),
            Cron.Daily, RecurringJobOptions);

        RecurringJob.AddOrUpdate(AuthKeyExpirationId, () => CheckExpiredOrExpiringAuthKeys(CancellationToken.None),
            Cron.Daily, RecurringJobOptions);

        RecurringJob.AddOrUpdate(EnsureSideNavId, () => EnsureSideNav(CancellationToken.None),
            Cron.Daily(1), RecurringJobOptions);


        RecurringJob.AddOrUpdate<IReadingHistoryService>(ReadingHistoryAggregationId,
            service => service.AggregateYesterdaysActivity(CancellationToken.None),
            "5 0 * * *", RecurringJobOptions); // 12:05 AM daily

        RecurringJob.AddOrUpdate<IActiveUserTrackerService>(FlushUserActiveTaskId,
            service => service.FlushAsync(CancellationToken.None),
            "*/5 * * * *", RecurringJobOptions);

    }

    private static bool IsInvalidCronSetting(string? setting)
    {
        return setting == null || (!NonCronOptions.Contains(setting) && !CronHelper.IsValidCron(setting));
    }

    public async Task ScheduleKavitaPlusTasks(CancellationToken cancellationToken = default)
    {
        if (!await _licenseService.HasActiveLicense(false, cancellationToken))
        {
            return;
        }

        RecurringJob.AddOrUpdate(CheckScrobblingTokensId,
            () => _scrobblingService.CheckExternalAccessTokens(CancellationToken.None),
            Cron.Daily, RecurringJobOptions);
        // We also kick off an immediate check on startup
        BackgroundJob.Enqueue(() => _scrobblingService.CheckExternalAccessTokens(CancellationToken.None));

        // Get the License Info (and cache it) on first load. This will internally cache the Github releases for the Version Service
        BackgroundJob.Enqueue(() => _licenseService.GetLicenseInfo(true, CancellationToken.None));  // Kick this off first to cache it then let it refresh every 9 hours (8 hour cache)

        // Spread out license checks for the backend
        var randomKPlusLicenseCheck = Rnd.Next(0, 60);
        RecurringJob.AddOrUpdate(LicenseCheckId, () => _licenseService.GetLicenseInfo(false, CancellationToken.None),
            $"{randomKPlusLicenseCheck} */9 * * *", RecurringJobOptions);

        // KavitaPlus Scrobbling (every hour) - randomise minutes to spread requests out for K+
        var randomMinute = Rnd.Next(0, 60);
        _logger.LogDebug("Scheduling KavitaPlus Scrobbling Task every hour @ {Minute}m", randomMinute);
        RecurringJob.AddOrUpdate(ProcessScrobblingEventsId,
            () => _scrobblingService.ProcessUpdatesSinceLastSync(CancellationToken.None),
            Cron.Hourly(randomMinute), RecurringJobOptions);
        RecurringJob.AddOrUpdate(ProcessProcessedScrobblingEventsId,
            () => _scrobblingService.ClearProcessedEvents(CancellationToken.None),
            Cron.Daily, RecurringJobOptions);

        // Backfilling/Freshening Reviews/Rating/Recommendations
        var randomKPlusBackfill = Rnd.Next(1, 5);
        RecurringJob.AddOrUpdate(KavitaPlusDataRefreshId,
            () => _externalMetadataService.FetchExternalDataTask(CancellationToken.None),
            Cron.Daily(randomKPlusBackfill), RecurringJobOptions);

        // This shouldn't be so close to fetching data due to Rate limit concerns
        var randomKPlusStackSync = Rnd.Next(6, 10);
        RecurringJob.AddOrUpdate(KavitaPlusStackSyncId,
            () => _smartCollectionSyncService.Sync(CancellationToken.None),
            Cron.Daily(randomKPlusStackSync), RecurringJobOptions);

        RecurringJob.AddOrUpdate(KavitaPlusWantToReadSyncId,
            () => _wantToReadSyncService.Sync(CancellationToken.None),
            Cron.Weekly(DayOfWeekHelper.Random()), RecurringJobOptions);

        RecurringJob.AddOrUpdate<IKavitaPlusAuditService>(PurgeKavitaPlusAuditLogsId,
            service => service.PurgeOldLogsAsync(CancellationToken.None),
            Cron.Daily, RecurringJobOptions);

        RecurringJob.AddOrUpdate<IScrobblingService>(TaskSchedulerConstants.CreateReadStatusTransitionRuleEventsId,
            service => service.RunReadStatusTransitionRules(CancellationToken.None),
            Cron.Daily, RecurringJobOptions);

        RecurringJob.AddOrUpdate<IOAuthService>(TaskSchedulerConstants.RefreshConnectedTokensId,
            service => service.RefreshTokens(CancellationToken.None),
            Cron.Daily, RecurringJobOptions);
    }


    /// <summary>
    /// Removes any Kavita+ Recurring Jobs
    /// </summary>
    public static void RemoveKavitaPlusTasks()
    {
        RecurringJob.RemoveIfExists(CheckScrobblingTokensId);
        RecurringJob.RemoveIfExists(LicenseCheckId);
        RecurringJob.RemoveIfExists(ProcessScrobblingEventsId);
        RecurringJob.RemoveIfExists(ProcessProcessedScrobblingEventsId);
        RecurringJob.RemoveIfExists(KavitaPlusDataRefreshId);
        RecurringJob.RemoveIfExists(KavitaPlusStackSyncId);
        RecurringJob.RemoveIfExists(KavitaPlusWantToReadSyncId);
        RecurringJob.RemoveIfExists(PurgeKavitaPlusAuditLogsId);
        RecurringJob.RemoveIfExists(TaskSchedulerConstants.CreateReadStatusTransitionRuleEventsId);
    }

    #region StatsTasks


    public async Task ScheduleStatsTasks(CancellationToken cancellationToken = default)
    {
        var allowStatCollection = (await _unitOfWork.SettingsRepository.GetSettingsDtoAsync(cancellationToken)).AllowStatCollection;
        if (!allowStatCollection)
        {
            _logger.LogDebug("User has opted out of stat collection, not registering tasks");
            RecurringJob.RemoveIfExists(ReportStatsTaskId);
            return;
        }

        var hour = Rnd.Next(0, 22);
        _logger.LogDebug("Scheduling stat collection daily at {Hour}:00", hour);

        RecurringJob.AddOrUpdate(ReportStatsTaskId, () => _statsService.Send(CancellationToken.None), Cron.Daily(hour), RecurringJobOptions);
    }


    /// <summary>
    /// Upon cancelling stat, we do report to the Stat service that we are no longer going to be reporting
    /// </summary>
    public void CancelStatsTasks()
    {
        _logger.LogDebug("Stopping Stat collection as user has opted out");
        RecurringJob.RemoveIfExists(ReportStatsTaskId);
        _statsService.SendCancellation();
    }

    /// <summary>
    /// First time run stat collection. Executes immediately on a background thread. Does not block.
    /// </summary>
    /// <remarks>Schedules it for 1 day in the future to ensure we don't have users that try the software out</remarks>
    public async Task RunStatCollection()
    {
        var allowStatCollection  = (await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).AllowStatCollection;
        if (!allowStatCollection)
        {
            _logger.LogDebug("User has opted out of stat collection, not sending stats");
            return;
        }
        BackgroundJob.Schedule(() => _statsService.Send(), DateTimeOffset.Now.AddDays(1));
    }

    public void ConvertAllCoversToEncoding()
    {
        if (HasAlreadyEnqueuedTask<IMediaConversionService>(s => s.ConvertAllManagedMediaToEncodingFormat(CancellationToken.None), checkRunningJobs: true)) return;

        BackgroundJob.Enqueue(() => _mediaConversionService.ConvertAllManagedMediaToEncodingFormat());
    }

    #endregion

    #region UpdateTasks

    public void ScheduleUpdaterTasks()
    {
        _logger.LogInformation("Scheduling Auto-Update tasks");
        RecurringJob.AddOrUpdate(CheckForUpdateId, () => CheckForUpdate(CancellationToken.None), $"0 */{Rnd.Next(4, 6)} * * *", RecurringJobOptions);
        BackgroundJob.Enqueue(() => CheckForUpdate(CancellationToken.None));
    }

    public async Task EnqueueScanFolderFromWatcherAsync(string changedPath, bool isDirectory)
    {
        var libraries = await _unitOfWork.LibraryRepository.GetLibraryDtosAsync();
        var target = ResolveScanFolder(changedPath, !isDirectory, libraries);
        if (target == null || target.Folder == target.Root)
        {
            _logger.LogInformation("[FolderWatchService] Change from {FilePath} could not find root level folder, ignoring change", changedPath);
            return;
        }

        var changedFolder = isDirectory ? changedPath : Path.GetDirectoryName(changedPath)!;
        await EnqueueScanFolderAsync(new ScanFolderRequest(target.LibraryId, target.Folder, changedFolder, true, false));
    }

    public async Task EnqueueScanFolderFromApiAsync(string path, bool abortOnNoSeriesMatch)
    {
        var libraries = await _unitOfWork.LibraryRepository.GetLibraryDtosAsync();
        var target = ResolveScanFolder(path, false, libraries);
        if (target == null)
        {
            _logger.LogWarning("Scan folder asked for {Path}, which is in no library. Dropping request", path.Sanitize());
            return;
        }

        await EnqueueScanFolderAsync(new ScanFolderRequest(target.LibraryId, target.Folder, path, false, abortOnNoSeriesMatch));
    }

    /// <summary>
    /// The deepest library root holding <paramref name="path"/> and the first folder under it, or the root when the
    /// path is the root. Null when no library holds the path
    /// </summary>
    internal static ScanFolderTarget? ResolveScanFolder(string path, bool isFile, IEnumerable<LibraryDto> libraries)
    {
        var libraryByRoot = new Dictionary<string, int>();
        foreach (var library in libraries)
        {
            foreach (var libraryRoot in library.Folders)
            {
                libraryByRoot.TryAdd(Parser.NormalizePath(libraryRoot), library.Id);
            }
        }

        var normalized = Parser.NormalizePath(path).TrimEnd('/');
        var root = normalized.DeepestContainingFolder(libraryByRoot.Keys);
        if (root == null) return null;

        var trimmedRoot = root.TrimEnd('/');
        var segments = normalized[trimmedRoot.Length..].Split('/', StringSplitOptions.RemoveEmptyEntries);
        var folderCount = isFile ? segments.Length - 1 : segments.Length;
        var folder = folderCount > 0 ? $"{trimmedRoot}/{segments[0]}" : root;

        return new ScanFolderTarget(libraryByRoot[root], root, folder);
    }

    /// <summary>
    /// Queues a <see cref="ScannerService.ScanFolder"/> job. A watcher request goes through
    /// <see cref="FolderValidationService.ValidateFolder"/> once its folder has been quiet for <see cref="WatcherQuietTime"/>, or by
    /// <see cref="WatcherScanCap"/> while changes keep coming. Any other request runs now, unless the same request is already waiting
    /// </summary>
    internal async Task EnqueueScanFolderAsync(ScanFolderRequest request)
    {
        var normalized = request with
        {
            Folder = Parser.NormalizePath(request.Folder),
            ChangedPath = string.IsNullOrEmpty(request.ChangedPath) ? string.Empty : Parser.NormalizePath(request.ChangedPath),
        };

        // The activity widget re-reads waiting folder scans only on ScanRescheduled, so it is sent when a folder starts
        // waiting or a waiting job's run time moves; otherwise the widget shows a time that has already passed
        var widgetMustRefresh = QueueFolderJob(normalized);
        if (!widgetMustRefresh) return;

        await _eventHub.SendMessageAsync(MessageFactory.ScanRescheduled, ScanQueueChangedEvent());
    }

    /// <returns>True when a folder started waiting or a waiting job's run time moved</returns>
    private bool QueueFolderJob(ScanFolderRequest request)
    {
        lock (ScanRequestLock)
        {
            var folderJobs = ScanJobQueue.Read().FolderJobs;
            var waiting = FindWaitingJob(request, folderJobs.Where(j => j.Request == request).ToList());
            if (waiting != WaitingJob.None)
            {
                _logger.LogTrace("Skipped scheduling ScanFolder for {Folder} as the same request is waiting", request.Folder);
                return waiting == WaitingJob.PushedBack;
            }

            var isNewFolder = folderJobs.All(j => j.Request.Folder != request.Folder);
            if (!request.FromFolderWatcher)
            {
                _logger.LogInformation("Scheduling ScanFolder for {Folder}", request.Folder);
                ScanJobQueue.Enqueue(() => _scannerService.ScanFolder(request));
                return isNewFolder;
            }

            _logger.LogInformation("Scheduling ValidateFolder for {Folder}", request.Folder);
            ScanJobQueue.Schedule<IFolderValidationService>(s => s.ValidateFolder(request, DateTime.UtcNow), WatcherQuietTime);
            return isNewFolder;
        }
    }

    /// <summary>
    /// A watcher request's waiting validation is pushed back unless it has reached the cap
    /// </summary>
    private static WaitingJob FindWaitingJob(ScanFolderRequest request, List<ScanFolderJob> sameRequest)
    {
        if (!request.FromFolderWatcher) return sameRequest.Count > 0 ? WaitingJob.Covers : WaitingJob.None;

        var validation = sameRequest.FirstOrDefault(j => j.RunAtUtc != null && j.FirstChangeUtc != null);
        if (validation == null) return WaitingJob.None;

        var delay = WatcherPushBack(validation.FirstChangeUtc!.Value, DateTime.UtcNow);
        if (delay <= TimeSpan.Zero) return WaitingJob.Covers;

        return ScanJobQueue.TryPushBack(validation.JobId, nameof(IFolderValidationService.ValidateFolder), delay)
            ? WaitingJob.PushedBack
            : WaitingJob.None;
    }

    /// <returns>How far from now a waiting watcher job moves; zero or less once it reached the cap</returns>
    internal static TimeSpan WatcherPushBack(DateTime firstChangeUtc, DateTime nowUtc)
    {
        var untilCap = firstChangeUtc + WatcherScanCap - nowUtc;
        return untilCap < WatcherQuietTime ? untilCap : WatcherQuietTime;
    }

    /// <summary>
    /// <see cref="MessageFactory.ScanRescheduled"/> with every delayed scan. The activity widget re-reads the snapshot on it,
    /// which is also how it learns the waiting ScanFolder jobs changed
    /// </summary>
    public static SignalRMessageDto ScanQueueChangedEvent()
    {
        return MessageFactory.ScanRescheduledEvent(GetScheduledScans(int.MaxValue).Scans);
    }

    #endregion

    public async Task CleanupDbEntries()
    {
        await _cleanupService.CleanupDbEntries();
    }

    /// <summary>
    /// Attempts to call ScanLibraries on ScannerService, but if another scan task is in progress, will reschedule the invocation for 3 hours in future.
    /// </summary>
    /// <param name="force"></param>
    public async Task EnqueueScanLibraries(bool force = false)
    {
        var runAt = DateTimeOffset.UtcNow.AddHours(ScanHourDelay);
        string delayedJobId;

        lock (ScanRequestLock)
        {
            var jobs = ScanJobQueue.Read().Jobs;
            if (jobs.FindWaiting(ScanTarget.AllLibraries, force) is { } waiting)
            {
                _logger.LogInformation("A scan of every library is already queued as job {JobId}. Skipping", waiting.JobId);
                return;
            }

            if (!jobs.IsScannerBusy())
            {
                ScanJobQueue.Enqueue(() => _scannerService.ScanLibraries(force));
                return;
            }

            delayedJobId = ScanJobQueue.Schedule(() => EnqueueScanLibraries(force), runAt);
        }

        _logger.LogInformation("A Scan is already running, rescheduling ScanLibraries in 3 hours as job {JobId}", delayedJobId);
        // Send InfoEvent to UI as this is invoked my API
        await _eventHub.SendMessageAsync(MessageFactory.Info, MessageFactory.ScanLibrariesDelayedEvent(runAt.UtcDateTime));
    }

    public async Task EnqueueScanLibrary(int libraryId, bool force = false)
    {
        var runAt = DateTimeOffset.UtcNow.AddHours(ScanHourDelay);
        string delayedJobId;

        lock (ScanRequestLock)
        {
            var jobs = ScanJobQueue.Read().Jobs;
            if (jobs.FindWaiting(ScanTarget.Library(libraryId), force) is { } waiting)
            {
                _logger.LogInformation("A scan of library {LibraryId} is already queued as job {JobId}. Skipping", libraryId, waiting.JobId);
                return;
            }

            if (!jobs.IsScannerBusy())
            {
                _logger.LogInformation("Enqueuing library scan for: {LibraryId}", libraryId);
                var jobId = ScanJobQueue.Enqueue(() => _scannerService.ScanLibrary(libraryId, force, true));
                // When we do a scan, force cache to re-unpack in case page numbers change (TODO: Make the scanner handle this)
                BackgroundJob.ContinueJobWith(jobId, () => _cleanupService.CleanupCacheDirectory());
                return;
            }

            delayedJobId = ScanJobQueue.Schedule(() => EnqueueScanLibrary(libraryId, force), runAt);
        }

        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId);
        _logger.LogInformation("A Scan is already running, rescheduling ScanLibrary for library {LibraryId} in 3 hours as job {JobId}",
            libraryId, delayedJobId);
        await _eventHub.SendMessageAsync(MessageFactory.Info,
            MessageFactory.ScanLibraryDelayedEvent(libraryId, library!.Name, runAt.UtcDateTime));
    }

    public void CleanupChapters(int[] chapterIds)
    {
        BackgroundJob.Enqueue(() => _cacheService.CleanupChapters(chapterIds));
    }

    public void RefreshMetadata(int libraryId, bool forceUpdate = true, bool forceColorscape = true)
    {
        var coveredByQueuedJob = AtLeast(forceUpdate)
            .SelectMany(force => AtLeast(forceColorscape).Select(colorscape => (force, colorscape)))
            .Any(flags => HasAlreadyEnqueuedTask<IMetadataService>(s =>
                s.GenerateCoversForLibrary(libraryId, flags.force, flags.colorscape, CancellationToken.None)));
        if (coveredByQueuedJob)
        {
            _logger.LogInformation("A duplicate request to refresh metadata for library occured. Skipping");
            return;
        }

        _logger.LogInformation("Enqueuing library metadata refresh for: {LibraryId}", libraryId);
        BackgroundJob.Enqueue(() => _metadataService.GenerateCoversForLibrary(libraryId, forceUpdate, forceColorscape));
    }

    /// <summary>
    /// Flag values that do at least what was asked: a forced job also covers an unforced request
    /// </summary>
    private static bool[] AtLeast(bool requested) => requested ? [true] : [false, true];

    public async Task RefreshSeriesMetadata(int libraryId, int seriesId, bool forceUpdate = false, bool forceColorscape = false)
    {
        var serverSettings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        if (HasAlreadyEnqueuedTask<IMetadataService>(s => s.GenerateCoversForSeries(serverSettings, libraryId, seriesId, forceUpdate, forceColorscape, CancellationToken.None)))
        {
            _logger.LogInformation("A duplicate request to refresh metadata for library occured. Skipping");
            return;
        }

        _logger.LogInformation("Enqueuing series metadata refresh for: {SeriesId}", seriesId);
        BackgroundJob.Enqueue(() => _metadataService.GenerateCoversForSeries(serverSettings, libraryId, seriesId, forceUpdate, forceColorscape));
    }

    public async Task EnqueueScanSeries(int libraryId, int seriesId, bool forceUpdate = false)
    {
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.None);
        if (series == null)
        {
            _logger.LogWarning("Series {SeriesId} not found, but a scan was requested. This should not happen", seriesId);
            return;
        }

        var runAt = DateTimeOffset.UtcNow.AddHours(3);
        string delayedJobId;

        lock (ScanRequestLock)
        {
            var jobs = ScanJobQueue.Read().Jobs;
            if (jobs.FindWaiting(ScanTarget.Series(libraryId, seriesId), forceUpdate) is { } waiting)
            {
                _logger.LogInformation("A scan of series {SeriesId} is already queued as job {JobId}. Skipping", seriesId, waiting.JobId);
                return;
            }

            if (!jobs.IsScannerBusy())
            {
                _logger.LogInformation("Enqueuing series scan for: {SeriesId}", seriesId);
                ScanJobQueue.Enqueue(() => _scannerService.ScanSeries(seriesId, forceUpdate));
                return;
            }

            delayedJobId = ScanJobQueue.Schedule(() => EnqueueScanSeries(libraryId, seriesId, forceUpdate), runAt);
        }

        _logger.LogInformation("A Scan is already running, rescheduling ScanSeries for series {SeriesId} ({SeriesName}) in 3 hours as job {JobId}",
            seriesId, series.Name, delayedJobId);
        await _eventHub.SendMessageAsync(MessageFactory.Info,
            MessageFactory.ScanSeriesDelayedEvent(libraryId, seriesId, series.Name, runAt.UtcDateTime));
    }

    public async Task RetimeDelayedScans()
    {
        RetimePlan plan;
        lock (ScanRequestLock)
        {
            var jobs = ScanJobQueue.Read().Jobs;
            // The scan still running will retime again when it ends
            if (jobs.IsScannerBusy() || jobs.HasScanDueBefore(DateTime.UtcNow + DelayedScanRetime.NextScanDelay))
            {
                _logger.LogDebug("A scan is running or about to, not retiming delayed scans yet");
                return;
            }

            plan = DelayedScanRetime.Plan(jobs.Delayed(), DateTime.UtcNow);
            // fromState: a job that started running since the read must not be pushed back into Scheduled
            foreach (var jobId in plan.Deletes)
            {
                ScanJobQueue.DeleteScheduled(jobId);
            }
            foreach (var move in plan.Moves)
            {
                ScanJobQueue.Reschedule(move.JobId, move.RunAtUtc);
            }
        }

        if (plan.IsEmpty)
        {
            _logger.LogDebug("No delayed scans to retime");
            return;
        }

        var (scans, _) = GetScheduledScans(int.MaxValue);
        var next = scans.FirstOrDefault();
        _logger.LogInformation(
            "Retimed {Moved} delayed scans, removed {Removed} duplicates, next: job {JobId} (library {LibraryId}, series {SeriesId}) at {RunAtUtc}",
            plan.Moves.Count, plan.Deletes.Count, next?.JobId, next?.LibraryId, next?.SeriesId, next?.RunAtUtc);

        await _eventHub.SendMessageAsync(MessageFactory.ScanRescheduled, MessageFactory.ScanRescheduledEvent(scans));
    }

    /// <summary>
    /// Calculates TimeToRead and bytes
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="seriesId"></param>
    /// <param name="forceUpdate"></param>
    public void AnalyzeFilesForSeries(int libraryId, int seriesId, bool forceUpdate = false)
    {
        if (HasAlreadyEnqueuedTask<IWordCountAnalyzerService>(s => s.ScanSeries(libraryId, seriesId, forceUpdate, CancellationToken.None)))
        {
            _logger.LogInformation("A duplicate request to scan series occured. Skipping");
            return;
        }

        _logger.LogInformation("Enqueuing analyze files scan for: {SeriesId}", seriesId);
        BackgroundJob.Enqueue(() => _wordCountAnalyzerService.ScanSeries(libraryId, seriesId, forceUpdate));
    }

    /// <summary>
    /// Not an external call. Only public so that we can call this for a Task
    /// </summary>
    // ReSharper disable once MemberCanBePrivate.Global
    public async Task CheckForUpdate(CancellationToken cancellationToken = default)
    {
        await _defaultRetryPolicy.ExecuteAsync(async () =>
        {
            var update = await _versionUpdaterService.CheckForUpdate(cancellationToken);
            if (update == null) return;

            await _versionUpdaterService.PushUpdate(update, cancellationToken);
        });
    }

    /// <summary>
    /// Checks for any user that does not have a Library Side nav, when they should
    /// </summary>
    /// <remarks>2 users reported this issue, I cannot reproduce, this is a precaution</remarks>
    public async Task EnsureSideNav(CancellationToken ct = default)
    {
        var users = await _unitOfWork.UserRepository.GetAllUsersAsync(AppUserIncludes.SideNavStreams, ct: ct);
        var libraries = (await _unitOfWork.LibraryRepository.GetLibrariesAsync(LibraryIncludes.AppUser, ct: ct)).ToList();
        var libraryLookup = libraries.ToDictionary(l => l.Id);

        // Build a lookup: userId -> set of library IDs they have access to
        var userLibraryAccess = new Dictionary<int, HashSet<int>>();
        foreach (var library in libraries)
        {
            foreach (var appUser in library.AppUsers)
            {
                if (!userLibraryAccess.TryGetValue(appUser.Id, out var libIds))
                {
                    libIds = [];
                    userLibraryAccess[appUser.Id] = libIds;
                }
                libIds.Add(library.Id);
            }
        }

        var hasChanges = false;

        foreach (var user in users)
        {
            // Get library IDs this user has access to
            if (!userLibraryAccess.TryGetValue(user.Id, out var accessibleLibraryIds))
            {
                continue; // User has no library access
            }

            // Get library IDs already in their SideNav
            var existingLibraryIds = user.SideNavStreams?
                .Where(s => s.LibraryId.HasValue)
                .Select(s => s.LibraryId!.Value)
                .ToHashSet() ?? [];

            var missingLibIds = accessibleLibraryIds.Except(existingLibraryIds).ToList();

            if (missingLibIds.Count == 0) continue;

            _logger.LogInformation(
                "Found {Count} libraries missing from User {UserId} sidenav: [{LibraryIds}]",
                missingLibIds.Count, user.Id, string.Join(", ", missingLibIds));

            foreach (var libId in missingLibIds)
            {
                user.CreateSideNavFromLibrary(libraryLookup[libId]);
            }

            _unitOfWork.UserRepository.Update(user);
            hasChanges = true;
        }

        if (hasChanges)
        {
            await _unitOfWork.CommitAsync(ct);
        }
    }

    /// <summary>
    /// Checks for soon to be expired and expired Auth keys and attempts to email the users
    /// </summary>
    public async Task CheckExpiredOrExpiringAuthKeys(CancellationToken ct = default)
    {
        var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        if (!settings.IsEmailSetup()) return;

        _logger.LogInformation("Checking for Expired or Expiring Auth Keys");
        var users = await _unitOfWork.UserRepository.GetAllUsersAsync(AppUserIncludes.AuthKeys, ct: ct);
        foreach (var user in users)
        {
            // Implies only the default keys
            if (user.AuthKeys.Count == 2) continue;

            // Check if key is expired
            var expiredKeys = user.AuthKeys
                .Where(k => k is {Provider: AuthKeyProvider.User, ExpiresAtUtc: not null} && k.ExpiresAtUtc <= DateTime.UtcNow)
                .ToList();
            var expiringSoonKeys = user.AuthKeys
                .Where(k => k is {Provider: AuthKeyProvider.User, ExpiresAtUtc: not null} && k.ExpiresAtUtc <= DateTime.UtcNow.Subtract(TimeSpan.FromDays(7)))
                .ToList();

            if (expiringSoonKeys.Count != 0)
            {
                var expiringSoonLatestDate = expiringSoonKeys.Max(k => k.ExpiresAtUtc);
                if (await ShouldSendAuthKeyExpirationReminder(user.Id, expiringSoonLatestDate!.Value, EmailService.AuthKeyExpiringSoonTemplate))
                {
                    await _emailService.SendAuthKeyExpiringSoonEmail(user.Id, expiringSoonKeys);
                }

            }

            if (expiredKeys.Count != 0)
            {
                var expiredLatestDate = expiredKeys.Max(k => k.ExpiresAtUtc);
                if (await ShouldSendAuthKeyExpirationReminder(user.Id, expiredLatestDate!.Value,
                        EmailService.AuthKeyExpiredTemplate))
                {
                    await _emailService.SendAuthKeyExpiredEmail(user.Id, expiredKeys);
                }

                foreach (var expiredKey in expiredKeys)
                {
                    await _authKeyService.InvalidateAsync(expiredKey.Key, ct);
                }
            }
        }
    }

    private async Task<bool> ShouldSendAuthKeyExpirationReminder(int userId, DateTime tokenExpiry, string emailTemplate)
    {
        if (tokenExpiry > DateTime.UtcNow) return false;

        var hasAlreadySentExpirationEmail = await _unitOfWork.DataContext.EmailHistory
            .AnyAsync(h => h.AppUserId == userId && h.Sent &&
                           h.EmailTemplate == emailTemplate &&
                           h.SendDate >= tokenExpiry);

        return !hasAlreadySentExpirationEmail;
    }

    /// <summary>
    /// Checks if the same call is already enqueued or scheduled. Pass the call exactly as it is enqueued:
    /// <c>HasAlreadyEnqueuedTask&lt;IMetadataService&gt;(s => s.GenerateCoversForLibrary(libraryId, true, true, CancellationToken.None))</c>
    /// </summary>
    /// <remarks>
    /// Arguments are compared as Hangfire serializes them, so a <see cref="CancellationToken"/> (stored as null) always
    /// matches and a DTO compares by value
    /// </remarks>
    /// <param name="job">The enqueued call, on the interface or class the job runs on</param>
    /// <param name="checkRunningJobs">Also match a job that is already running</param>
    /// <param name="queue">Queue to look in for enqueued jobs</param>
    public static bool HasAlreadyEnqueuedTask<T>(Expression<Func<T, Task>> job, bool checkRunningJobs = false, string queue = DefaultQueue)
    {
        var wanted = Job.FromExpression(job);
        var wantedArgs = InvocationData.SerializeJob(wanted).Arguments;
        var monitoring = JobStorage.Current.GetMonitoringApi();

        if (monitoring.EnqueuedJobs(queue, 0, int.MaxValue).Exists(j => j.Value.InEnqueuedState && IsSameCall(j.Value.Job))) return true;
        if (monitoring.ScheduledJobs(0, int.MaxValue).Exists(j => IsSameCall(j.Value.Job))) return true;

        return checkRunningJobs && monitoring.ProcessingJobs(0, int.MaxValue).Exists(j => IsSameCall(j.Value.Job));

        // Hangfire stores the class DI resolved (MetadataService), not the interface the call names
        bool IsSameCall(Job? stored) =>
            stored != null &&
            stored.Type.IsAssignableTo(typeof(T)) &&
            stored.Method.Name == wanted.Method.Name &&
            InvocationData.SerializeJob(stored).Arguments == wantedArgs;
    }


    public static bool IsMethodRunningOrEnqueued(string methodName, string queue = DefaultQueue)
    {
        using var connection = JobStorage.Current.GetConnection();

        var monitoring = JobStorage.Current.GetMonitoringApi();

        var processing = monitoring.ProcessingJobs(0, 100);
        if (processing.Any(j => j.Value.Job?.Method.Name == methodName))
        {
            return true;
        }

        var enqueued = monitoring.EnqueuedJobs(queue, 0, 100);
        if (enqueued.Any(j => j.Value.Job?.Method.Name == methodName))
        {
            return true;
        }

        return false;
    }

    public static IReadOnlySet<string> GetProcessingJobIds()
    {
        return JobStorage.Current.GetMonitoringApi().ProcessingJobs(0, int.MaxValue)
            .Select(j => j.Key)
            .ToHashSet();
    }

    /// <summary>
    /// Scans delayed by <see cref="EnqueueScanLibraries"/>, <see cref="EnqueueScanLibrary"/> or <see cref="EnqueueScanSeries"/> because another scan was running
    /// </summary>
    public static (IList<ScheduledScanDto> Scans, int Total) GetScheduledScans(int take)
    {
        var scans = ScanJobQueue.Read().Jobs.Delayed()
            .OrderBy(j => j.RunAtUtc)
            .ThenBy(j => j.CreatedAtUtc)
            .Select(ToScheduledScan)
            .ToList();

        return (scans.Take(take).ToList(), scans.Count);
    }

    /// <summary>
    /// Waiting <see cref="ScannerService.ScanFolder"/> and <see cref="FolderValidationService.ValidateFolder"/> jobs, one entry per
    /// library and trigger, soonest first
    /// </summary>
    public static IList<ScheduledFolderScanDto> GetScheduledFolderScans(IEnumerable<LibraryDto> libraries)
    {
        var libraryIds = libraries.Select(l => l.Id).ToHashSet();

        return ScanJobQueue.Read().FolderJobs
            .GroupBy(j => (LibraryId: libraryIds.Contains(j.Request.LibraryId) ? j.Request.LibraryId : (int?) null, j.Request.FromFolderWatcher))
            .Select(group =>
            {
                var folders = group
                    .GroupBy(j => j.Request.Folder)
                    .Select(f => (Folder: f.Key, RunAtUtc: f.Min(j => j.RunAtUtc ?? DateTime.MinValue)))
                    .OrderBy(f => f.RunAtUtc)
                    .ThenBy(f => f.Folder, StringComparer.Ordinal)
                    .ToList();
                var runAtUtc = folders[0].RunAtUtc;

                return new ScheduledFolderScanDto
                {
                    LibraryId = group.Key.LibraryId,
                    FromFolderWatcher = group.Key.FromFolderWatcher,
                    RunAtUtc = runAtUtc == DateTime.MinValue ? null : runAtUtc,
                    Folders = folders.Take(ScheduledFolderScanDto.MaxFolders).Select(f => f.Folder).ToList(),
                    FolderCount = folders.Count,
                };
            })
            .OrderBy(s => s.RunAtUtc ?? DateTime.MinValue)
            .ToList();
    }

    private static ScheduledScanDto ToScheduledScan(ScanJob job)
    {
        return new ScheduledScanDto
        {
            JobId = job.JobId,
            LibraryId = job.Target.LibraryId,
            SeriesId = job.Target.SeriesId,
            RunAtUtc = job.RunAtUtc.GetValueOrDefault(),
        };
    }

    /// <summary>
    /// Next run of the recurring tasks the activity widget shows. A disabled task has no next run and is left out
    /// </summary>
    public static IList<UpcomingTaskDto> GetUpcomingTasks()
    {
        string[] taskIds = [ScanLibrariesTaskId, CleanupTaskId, BackupTaskId, TaskCblSyncId];

        using var connection = JobStorage.Current.GetConnection();
        return connection.GetRecurringJobs(taskIds)
            .Where(j => j.NextExecution.HasValue)
            .Select(j => new UpcomingTaskDto
            {
                TaskId = j.Id,
                Cron = j.Cron,
                NextRunUtc = DateTime.SpecifyKind(j.NextExecution!.Value, DateTimeKind.Utc),
            })
            .OrderBy(t => t.NextRunUtc)
            .ToList();
    }

    /// <summary>
    /// Returns Utc DateTime of next run of a given Job Id
    /// </summary>
    /// <param name="jobId"></param>
    /// <returns></returns>
    public static DateTime? GetNextRun(string jobId)
    {
        using var connection = JobStorage.Current.GetConnection();
        var job = connection.GetRecurringJobs()
            .FirstOrDefault(j => j.Id == jobId);

        return job?.NextExecution;
    }

    internal sealed record ScanFolderTarget(int LibraryId, string Root, string Folder);

    /// <summary>
    /// What a folder scan request found already waiting for the same request
    /// </summary>
    private enum WaitingJob
    {
        /// <summary>
        /// Nothing waits for this request, or the waiting validation already started, so a new job is queued
        /// </summary>
        [Description("None")]
        None = 0,
        /// <summary>
        /// A waiting job already covers the request: an identical API request, or a watcher validation at its 10 minute cap
        /// </summary>
        [Description("Covers")]
        Covers = 1,
        /// <summary>
        /// A waiting watcher validation was moved back by the quiet time, so its run time changed
        /// </summary>
        [Description("Pushed Back")]
        PushedBack = 2,
    }
}
