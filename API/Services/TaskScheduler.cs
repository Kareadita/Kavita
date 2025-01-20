using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers.Converters;
using API.Services.Plus;
using API.Services.Tasks;
using API.Services.Tasks.Metadata;
using API.SignalR;
using Hangfire;
using Kavita.Common.Helpers;
using Microsoft.Extensions.Logging;

namespace API.Services;

public interface ITaskScheduler
{
    Task ScheduleTasks();
    Task ScheduleStatsTasks();
    void ScheduleUpdaterTasks();
    Task ScheduleKavitaPlusTasks();
    void ScanFolder(string folderPath, string originalPath, TimeSpan delay);
    void ScanFolder(string folderPath);
    Task ScanLibrary(int libraryId, bool force = false);
    Task ScanLibraries(bool force = false);
    void CleanupChapters(int[] chapterIds);
    void RefreshMetadata(int libraryId, bool forceUpdate = true, bool forceColorscape = true);
    void RefreshSeriesMetadata(int libraryId, int seriesId, bool forceUpdate = false, bool forceColorscape = false);
    Task ScanSeries(int libraryId, int seriesId, bool forceUpdate = false);
    void AnalyzeFilesForSeries(int libraryId, int seriesId, bool forceUpdate = false);
    void AnalyzeFilesForLibrary(int libraryId, bool forceUpdate = false);
    void CancelStatsTasks();
    Task RunStatCollection();
    void CovertAllCoversToEncoding();
    Task CleanupDbEntries();
    Task CheckForUpdate();
    Task SyncThemes();
}
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
    private readonly IThemeService _themeService;
    private readonly IWordCountAnalyzerService _wordCountAnalyzerService;
    private readonly IStatisticService _statisticService;
    private readonly IMediaConversionService _mediaConversionService;
    private readonly IScrobblingService _scrobblingService;
    private readonly ILicenseService _licenseService;
    private readonly IExternalMetadataService _externalMetadataService;
    private readonly ISmartCollectionSyncService _smartCollectionSyncService;
    private readonly IWantToReadSyncService _wantToReadSyncService;
    private readonly IEventHub _eventHub;

    public static BackgroundJobServer Client => new ();
    public const string ScanQueue = "scan";
    public const string DefaultQueue = "default";
    public const string RemoveFromWantToReadTaskId = "remove-from-want-to-read";
    public const string UpdateYearlyStatsTaskId = "update-yearly-stats";
    public const string SyncThemesTaskId = "sync-themes";
    public const string CheckForUpdateId = "check-updates";
    public const string CleanupDbTaskId = "cleanup-db";
    public const string CleanupTaskId = "cleanup";
    public const string BackupTaskId = "backup";
    public const string ScanLibrariesTaskId = "scan-libraries";
    public const string ReportStatsTaskId = "report-stats";
    public const string CheckScrobblingTokensId = "check-scrobbling-tokens";
    public const string ProcessScrobblingEventsId = "process-scrobbling-events";
    public const string ProcessProcessedScrobblingEventsId = "process-processed-scrobbling-events";
    public const string LicenseCheckId = "license-check";
    public const string KavitaPlusDataRefreshId = "kavita+-data-refresh";
    public const string KavitaPlusStackSyncId = "kavita+-stack-sync";
    public const string KavitaPlusWantToReadSyncId = "kavita+-want-to-read-sync";

    public static readonly ImmutableArray<string> ScanTasks =
        ["ScannerService", "ScanLibrary", "ScanLibraries", "ScanFolder", "ScanSeries"];
    private static readonly ImmutableArray<string> NonCronOptions = ["disabled", "daily", "weekly"];

    private static readonly Random Rnd = new Random();

    private static readonly RecurringJobOptions RecurringJobOptions = new RecurringJobOptions()
    {
        TimeZone = TimeZoneInfo.Local
    };


    public TaskScheduler(ICacheService cacheService, ILogger<TaskScheduler> logger, IScannerService scannerService,
        IUnitOfWork unitOfWork, IMetadataService metadataService, IBackupService backupService,
        ICleanupService cleanupService, IStatsService statsService, IVersionUpdaterService versionUpdaterService,
        IThemeService themeService, IWordCountAnalyzerService wordCountAnalyzerService, IStatisticService statisticService,
        IMediaConversionService mediaConversionService, IScrobblingService scrobblingService, ILicenseService licenseService,
        IExternalMetadataService externalMetadataService, ISmartCollectionSyncService smartCollectionSyncService,
        IWantToReadSyncService wantToReadSyncService, IEventHub eventHub)
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
        _themeService = themeService;
        _wordCountAnalyzerService = wordCountAnalyzerService;
        _statisticService = statisticService;
        _mediaConversionService = mediaConversionService;
        _scrobblingService = scrobblingService;
        _licenseService = licenseService;
        _externalMetadataService = externalMetadataService;
        _smartCollectionSyncService = smartCollectionSyncService;
        _wantToReadSyncService = wantToReadSyncService;
        _eventHub = eventHub;
    }

    public async Task ScheduleTasks()
    {
        _logger.LogInformation("Scheduling reoccurring tasks");


        var setting = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.TaskScan)).Value;
        if (IsInvalidCronSetting(setting))
        {
            _logger.LogError("Scan Task has invalid cron, defaulting to Daily");
            RecurringJob.AddOrUpdate(ScanLibrariesTaskId, () => ScanLibraries(false),
                Cron.Daily, RecurringJobOptions);
        }
        else
        {
            var scanLibrarySetting = setting;
            _logger.LogDebug("Scheduling Scan Library Task for {Setting}", scanLibrarySetting);
            RecurringJob.AddOrUpdate(ScanLibrariesTaskId, () => ScanLibraries(false),
                () => CronConverter.ConvertToCronNotation(scanLibrarySetting), RecurringJobOptions);
        }


        setting = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.TaskBackup)).Value;
        if (IsInvalidCronSetting(setting))
        {
            _logger.LogError("Backup Task has invalid cron, defaulting to Weekly");
            RecurringJob.AddOrUpdate(BackupTaskId, () => _backupService.BackupDatabase(),
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
            RecurringJob.AddOrUpdate(BackupTaskId, () => _backupService.BackupDatabase(),
                () => schedule, RecurringJobOptions);
        }

        setting = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.TaskCleanup)).Value;
        if (IsInvalidCronSetting(setting))
        {
            _logger.LogError("Cleanup Task has invalid cron, defaulting to Daily");
            RecurringJob.AddOrUpdate(CleanupTaskId, () => _cleanupService.Cleanup(),
                Cron.Daily, RecurringJobOptions);
        }
        else
        {
            _logger.LogDebug("Scheduling Cleanup Task for {Setting}", setting);
            RecurringJob.AddOrUpdate(CleanupTaskId, () => _cleanupService.Cleanup(),
                CronConverter.ConvertToCronNotation(setting), RecurringJobOptions);
        }


        RecurringJob.AddOrUpdate(RemoveFromWantToReadTaskId, () => _cleanupService.CleanupWantToRead(),
            Cron.Daily, RecurringJobOptions);
        RecurringJob.AddOrUpdate(UpdateYearlyStatsTaskId, () => _statisticService.UpdateServerStatistics(),
            Cron.Monthly, RecurringJobOptions);

        RecurringJob.AddOrUpdate(SyncThemesTaskId, () => SyncThemes(),
            Cron.Daily, RecurringJobOptions);

        await ScheduleKavitaPlusTasks();
    }

    private static bool IsInvalidCronSetting(string setting)
    {
        return setting == null || (!NonCronOptions.Contains(setting) && !CronHelper.IsValidCron(setting));
    }

    public async Task ScheduleKavitaPlusTasks()
    {
        // KavitaPlus based (needs license check)
        var license = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey)).Value;
        if (string.IsNullOrEmpty(license) || !await _licenseService.HasActiveSubscription(license))
        {
            return;
        }

        RecurringJob.AddOrUpdate(CheckScrobblingTokensId, () => _scrobblingService.CheckExternalAccessTokens(),
            Cron.Daily, RecurringJobOptions);
        BackgroundJob.Enqueue(() => _scrobblingService.CheckExternalAccessTokens()); // We also kick off an immediate check on startup

        // Get the License Info (and cache it) on first load. This will internally cache the Github releases for the Version Service
        await _licenseService.GetLicenseInfo(true); // Kick this off first to cache it then let it refresh every 9 hours (8 hour cache)
        RecurringJob.AddOrUpdate(LicenseCheckId, () => _licenseService.GetLicenseInfo(false),
            LicenseService.Cron, RecurringJobOptions);

        // KavitaPlus Scrobbling (every 4 hours)
        RecurringJob.AddOrUpdate(ProcessScrobblingEventsId, () => _scrobblingService.ProcessUpdatesSinceLastSync(),
            "0 */1 * * *", RecurringJobOptions);
        RecurringJob.AddOrUpdate(ProcessProcessedScrobblingEventsId, () => _scrobblingService.ClearProcessedEvents(),
            Cron.Daily, RecurringJobOptions);

        // Backfilling/Freshening Reviews/Rating/Recommendations
        RecurringJob.AddOrUpdate(KavitaPlusDataRefreshId,
            () => _externalMetadataService.FetchExternalDataTask(), Cron.Daily(Rnd.Next(1, 4)),
            RecurringJobOptions);

        // This shouldn't be so close to fetching data due to Rate limit concerns
        RecurringJob.AddOrUpdate(KavitaPlusStackSyncId,
            () => _smartCollectionSyncService.Sync(), Cron.Daily(Rnd.Next(6, 10)),
            RecurringJobOptions);

        RecurringJob.AddOrUpdate(KavitaPlusWantToReadSyncId,
            () => _wantToReadSyncService.Sync(), Cron.Weekly(DayOfWeekHelper.Random()),
            RecurringJobOptions);
    }

    #region StatsTasks


    public async Task ScheduleStatsTasks()
    {
        var allowStatCollection = (await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).AllowStatCollection;
        if (!allowStatCollection)
        {
            _logger.LogDebug("User has opted out of stat collection, not registering tasks");
            return;
        }

        _logger.LogDebug("Scheduling stat collection daily");
        RecurringJob.AddOrUpdate(ReportStatsTaskId, () => _statsService.Send(), Cron.Daily(Rnd.Next(0, 22)), RecurringJobOptions);
    }

    public void AnalyzeFilesForLibrary(int libraryId, bool forceUpdate = false)
    {
        _logger.LogInformation("Enqueuing library file analysis for: {LibraryId}", libraryId);
        BackgroundJob.Enqueue(() => _wordCountAnalyzerService.ScanLibrary(libraryId, forceUpdate));
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

    public void CovertAllCoversToEncoding()
    {
        var defaultParams = Array.Empty<object>();
        if (MediaConversionService.ConversionMethods.Any(method =>
                HasAlreadyEnqueuedTask(MediaConversionService.Name, method, defaultParams, DefaultQueue, true)))
        {
            return;
        }

        BackgroundJob.Enqueue(() => _mediaConversionService.ConvertAllManagedMediaToEncodingFormat());
    }

    #endregion

    #region UpdateTasks

    public void ScheduleUpdaterTasks()
    {
        _logger.LogInformation("Scheduling Auto-Update tasks");
        RecurringJob.AddOrUpdate(CheckForUpdateId, () => CheckForUpdate(), $"0 */{Rnd.Next(4, 6)} * * *", RecurringJobOptions);
        BackgroundJob.Enqueue(() => CheckForUpdate());
    }

    /// <summary>
    /// Queue up a Scan folder for a folder from Library Watcher.
    /// </summary>
    /// <param name="folderPath"></param>
    /// <param name="originalPath"></param>
    /// <param name="delay"></param>
    public void ScanFolder(string folderPath, string originalPath, TimeSpan delay)
    {
        var normalizedFolder = Tasks.Scanner.Parser.Parser.NormalizePath(folderPath);
        var normalizedOriginal = Tasks.Scanner.Parser.Parser.NormalizePath(originalPath);

        if (HasAlreadyEnqueuedTask(ScannerService.Name, "ScanFolder", [normalizedFolder, normalizedOriginal]) ||
            HasAlreadyEnqueuedTask(ScannerService.Name, "ScanFolder", [normalizedFolder, string.Empty]))
        {
            _logger.LogDebug("Skipped scheduling ScanFolder for {Folder} as a job already queued",
                normalizedFolder);
            return;
        }

        // Not sure where we should put this code, but we can get a bunch of ScanFolders when original has slight variations, like
        // create a folder, add a new file, etc. All of these can be merged into just 1 request.

        _logger.LogInformation("Scheduling ScanFolder for {Folder}", normalizedFolder);
        BackgroundJob.Schedule(() => _scannerService.ScanFolder(normalizedFolder, normalizedOriginal), delay);
    }

    public void ScanFolder(string folderPath)
    {
        var normalizedFolder = Tasks.Scanner.Parser.Parser.NormalizePath(folderPath);
        if (HasAlreadyEnqueuedTask(ScannerService.Name, "ScanFolder", [normalizedFolder, string.Empty]))
        {
            _logger.LogDebug("Skipped scheduling ScanFolder for {Folder} as a job already queued",
                normalizedFolder);
            return;
        }

        _logger.LogInformation("Scheduling ScanFolder for {Folder}", normalizedFolder);
        _scannerService.ScanFolder(normalizedFolder, string.Empty);
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
    public async Task ScanLibraries(bool force = false)
    {
        if (RunningAnyTasksByMethod(ScanTasks, ScanQueue))
        {
            _logger.LogInformation("A Scan is already running, rescheduling ScanLibraries in 3 hours");
            // Send InfoEvent to UI as this is invoked my API
            BackgroundJob.Schedule(() => ScanLibraries(force), TimeSpan.FromHours(3));
            await _eventHub.SendMessageAsync(MessageFactory.Info, MessageFactory.InfoEvent($"Scan libraries task delayed",
                $"A scan was ongoing during processing of the scan libraries task. Task has been rescheduled for 3 hours: {DateTime.Now.AddHours(3)}"));
            return;
        }
        BackgroundJob.Enqueue(() => _scannerService.ScanLibraries(force));
    }

    public async Task ScanLibrary(int libraryId, bool force = false)
    {
        if (HasScanTaskRunningForLibrary(libraryId))
        {
            _logger.LogInformation("A duplicate request for Library Scan on library {LibraryId} occured. Skipping", libraryId);
            return;
        }
        if (RunningAnyTasksByMethod(ScanTasks, ScanQueue))
        {
            var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId);
            _logger.LogInformation("A Scan is already running, rescheduling ScanLibrary in 3 hours");
            await _eventHub.SendMessageAsync(MessageFactory.Info, MessageFactory.InfoEvent($"Scan library task delayed",
                $"A scan was ongoing during processing of the {library!.Name} scan task. Task has been rescheduled for 3 hours: {DateTime.Now.AddHours(3)}"));
            BackgroundJob.Schedule(() => ScanLibrary(libraryId, force), TimeSpan.FromHours(3));
            return;
        }

        _logger.LogInformation("Enqueuing library scan for: {LibraryId}", libraryId);
        var jobId = BackgroundJob.Enqueue(() => _scannerService.ScanLibrary(libraryId, force, true));
        // When we do a scan, force cache to re-unpack in case page numbers change
        BackgroundJob.ContinueJobWith(jobId, () => _cleanupService.CleanupCacheDirectory());
    }

    public void TurnOnScrobbling(int userId = 0)
    {
        BackgroundJob.Enqueue(() => _scrobblingService.CreateEventsFromExistingHistory(userId));
    }

    public void CleanupChapters(int[] chapterIds)
    {
        BackgroundJob.Enqueue(() => _cacheService.CleanupChapters(chapterIds));
    }

    public void RefreshMetadata(int libraryId, bool forceUpdate = true, bool forceColorscape = true)
    {
        var alreadyEnqueued = HasAlreadyEnqueuedTask(MetadataService.Name, "GenerateCoversForLibrary",
                                  [libraryId, true, true]) ||
                              HasAlreadyEnqueuedTask("MetadataService", "GenerateCoversForLibrary",
                                  [libraryId, false, false]);
        if (alreadyEnqueued)
        {
            _logger.LogInformation("A duplicate request to refresh metadata for library occured. Skipping");
            return;
        }

        _logger.LogInformation("Enqueuing library metadata refresh for: {LibraryId}", libraryId);
        BackgroundJob.Enqueue(() => _metadataService.GenerateCoversForLibrary(libraryId, forceUpdate, forceColorscape));
    }

    public void RefreshSeriesMetadata(int libraryId, int seriesId, bool forceUpdate = false, bool forceColorscape = false)
    {
        if (HasAlreadyEnqueuedTask(MetadataService.Name,"GenerateCoversForSeries", [libraryId, seriesId, forceUpdate, forceColorscape]))
        {
            _logger.LogInformation("A duplicate request to refresh metadata for library occured. Skipping");
            return;
        }

        _logger.LogInformation("Enqueuing series metadata refresh for: {SeriesId}", seriesId);
        BackgroundJob.Enqueue(() => _metadataService.GenerateCoversForSeries(libraryId, seriesId, forceUpdate, forceColorscape));
    }

    public async Task ScanSeries(int libraryId, int seriesId, bool forceUpdate = false)
    {
        if (HasAlreadyEnqueuedTask(ScannerService.Name, "ScanSeries", [seriesId, forceUpdate], ScanQueue))
        {
            _logger.LogInformation("A duplicate request to scan series occured. Skipping");
            return;
        }
        if (RunningAnyTasksByMethod(ScanTasks, ScanQueue))
        {
            // BUG: This can end up triggering a ton of scan series calls (but i haven't seen in practice)
            var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.None);
            _logger.LogInformation("A Scan is already running, rescheduling ScanSeries in 10 minutes");
            await _eventHub.SendMessageAsync(MessageFactory.Info, MessageFactory.InfoEvent($"Scan series task delayed: {series!.Name}",
                $"A scan was ongoing during processing of the scan series task. Task has been rescheduled for 10 minutes: {DateTime.Now.AddMinutes(10)}"));
            BackgroundJob.Schedule(() => ScanSeries(libraryId, seriesId, forceUpdate), TimeSpan.FromMinutes(10));
            return;
        }

        _logger.LogInformation("Enqueuing series scan for: {SeriesId}", seriesId);
        BackgroundJob.Enqueue(() => _scannerService.ScanSeries(seriesId, forceUpdate));
    }

    /// <summary>
    /// Calculates TimeToRead and bytes
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="seriesId"></param>
    /// <param name="forceUpdate"></param>
    public void AnalyzeFilesForSeries(int libraryId, int seriesId, bool forceUpdate = false)
    {
        if (HasAlreadyEnqueuedTask("WordCountAnalyzerService", "ScanSeries", [libraryId, seriesId, forceUpdate]))
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
    public async Task CheckForUpdate()
    {
        var update = await _versionUpdaterService.CheckForUpdate();
        if (update == null) return;
        await _versionUpdaterService.PushUpdate(update);
    }

    public async Task SyncThemes()
    {
        await _themeService.SyncThemes();
    }

    /// <summary>
    /// If there is an enqueued or scheduled task for <see cref="ScannerService.ScanLibrary"/> method
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="checkRunningJobs">Checks against jobs currently executing as well</param>
    /// <returns></returns>
    public static bool HasScanTaskRunningForLibrary(int libraryId, bool checkRunningJobs = true)
    {
        return
            HasAlreadyEnqueuedTask(ScannerService.Name, "ScanLibrary", [libraryId, true, true], ScanQueue,
                checkRunningJobs) ||
            HasAlreadyEnqueuedTask(ScannerService.Name, "ScanLibrary", [libraryId, false, true], ScanQueue,
                checkRunningJobs) ||
            HasAlreadyEnqueuedTask(ScannerService.Name, "ScanLibrary", [libraryId, true, false], ScanQueue,
                checkRunningJobs) ||
            HasAlreadyEnqueuedTask(ScannerService.Name, "ScanLibrary", [libraryId, false, false], ScanQueue,
                checkRunningJobs);
    }

    /// <summary>
    /// If there is an enqueued or scheduled task for <see cref="ScannerService.ScanSeries"/> method
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="checkRunningJobs">Checks against jobs currently executing as well</param>
    /// <returns></returns>
    public static bool HasScanTaskRunningForSeries(int seriesId, bool checkRunningJobs = true)
    {
        return
            HasAlreadyEnqueuedTask(ScannerService.Name, "ScanSeries", [seriesId, true], ScanQueue, checkRunningJobs) ||
            HasAlreadyEnqueuedTask(ScannerService.Name, "ScanSeries", [seriesId, false], ScanQueue, checkRunningJobs);
    }


    /// <summary>
    /// Checks if this same invocation is already enqueued or scheduled
    /// </summary>
    /// <param name="methodName">Method name that was enqueued</param>
    /// <param name="className">Class name the method resides on</param>
    /// <param name="args">object[] of arguments in the order they are passed to enqueued job</param>
    /// <param name="queue">Queue to check against. Defaults to "default"</param>
    /// <param name="checkRunningJobs">Check against running jobs. Defaults to false.</param>
    /// <param name="checkArgs">Check against arguments. Defaults to true.</param>
    /// <returns></returns>
    public static bool HasAlreadyEnqueuedTask(string className, string methodName, object[] args, string queue = DefaultQueue, bool checkRunningJobs = false)
    {
        var enqueuedJobs =  JobStorage.Current.GetMonitoringApi().EnqueuedJobs(queue, 0, int.MaxValue);
        var ret = enqueuedJobs.Exists(j => j.Value.InEnqueuedState &&
                                           j.Value.Job.Method.DeclaringType != null && j.Value.Job.Args.SequenceEqual(args) &&
                                           j.Value.Job.Method.Name.Equals(methodName) &&
                                           j.Value.Job.Method.DeclaringType.Name.Equals(className));
        if (ret) return true;

        var scheduledJobs = JobStorage.Current.GetMonitoringApi().ScheduledJobs(0, int.MaxValue);
        ret = scheduledJobs.Exists(j =>
            j.Value.Job != null &&
            j.Value.Job.Method.DeclaringType != null && j.Value.Job.Args.SequenceEqual(args) &&
            j.Value.Job.Method.Name.Equals(methodName) &&
            j.Value.Job.Method.DeclaringType.Name.Equals(className));

        if (ret) return true;

        if (checkRunningJobs)
        {
            var runningJobs = JobStorage.Current.GetMonitoringApi().ProcessingJobs(0, int.MaxValue);
            return runningJobs.Exists(j =>
                j.Value.Job.Method.DeclaringType != null && j.Value.Job.Args.SequenceEqual(args) &&
                j.Value.Job.Method.Name.Equals(methodName) &&
                j.Value.Job.Method.DeclaringType.Name.Equals(className));
        }

        return false;
    }


    /// <summary>
    /// Checks against any jobs that are running or about to run
    /// </summary>
    /// <param name="classNames"></param>
    /// <param name="queue"></param>
    /// <returns></returns>
    public static bool RunningAnyTasksByMethod(IEnumerable<string> classNames, string queue = DefaultQueue)
    {
        var enqueuedJobs =  JobStorage.Current.GetMonitoringApi().EnqueuedJobs(queue, 0, int.MaxValue);
        var ret = enqueuedJobs.Exists(j => !j.Value.InEnqueuedState &&
                                     classNames.Contains(j.Value.Job.Method.DeclaringType?.Name));
        if (ret) return true;

        var runningJobs = JobStorage.Current.GetMonitoringApi().ProcessingJobs(0, int.MaxValue);
        return runningJobs.Exists(j => classNames.Contains(j.Value.Job.Method.DeclaringType?.Name));
    }
}
