using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using API.Entities.Enums;
using API.Helpers.Converters;
using API.Interfaces;
using API.Interfaces.Services;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace API.Services
{
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
        private const string SendDataTask = "finalize-stats";

        public static BackgroundJobServer Client => new BackgroundJobServer();


        public TaskScheduler(ICacheService cacheService, ILogger<TaskScheduler> logger, IScannerService scannerService,
            IUnitOfWork unitOfWork, IMetadataService metadataService, IBackupService backupService,
            ICleanupService cleanupService, IStatsService statsService, IVersionUpdaterService versionUpdaterService)
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
        }

        public void ScheduleTasks()
        {
            _logger.LogInformation("Scheduling reoccurring tasks");

            var setting = Task.Run(() => _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.TaskScan)).GetAwaiter().GetResult().Value;
            if (setting != null)
            {
                var scanLibrarySetting = setting;
                _logger.LogDebug("Scheduling Scan Library Task for {Setting}", scanLibrarySetting);
                RecurringJob.AddOrUpdate("scan-libraries", () => _scannerService.ScanLibraries(),
                    () => CronConverter.ConvertToCronNotation(scanLibrarySetting), TimeZoneInfo.Local);
            }
            else
            {
                RecurringJob.AddOrUpdate("scan-libraries", () => _scannerService.ScanLibraries(), Cron.Daily, TimeZoneInfo.Local);
            }

            setting = Task.Run(() => _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.TaskBackup)).Result.Value;
            if (setting != null)
            {
                _logger.LogDebug("Scheduling Backup Task for {Setting}", setting);
                RecurringJob.AddOrUpdate("backup", () => _backupService.BackupDatabase(), () => CronConverter.ConvertToCronNotation(setting), TimeZoneInfo.Local);
            }
            else
            {
                RecurringJob.AddOrUpdate("backup", () => _backupService.BackupDatabase(), Cron.Weekly, TimeZoneInfo.Local);
            }

            RecurringJob.AddOrUpdate("cleanup", () => _cleanupService.Cleanup(), Cron.Daily, TimeZoneInfo.Local);

            RecurringJob.AddOrUpdate("check-for-updates", () => _scannerService.ScanLibraries(), Cron.Daily, TimeZoneInfo.Local);
        }

        #region StatsTasks


        public async Task ScheduleStatsTasks()
        {
            var allowStatCollection  = (await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).AllowStatCollection;
            if (!allowStatCollection)
            {
                _logger.LogDebug("User has opted out of stat collection, not registering tasks");
                return;
            }

            _logger.LogDebug("Scheduling stat collection daily");
            RecurringJob.AddOrUpdate(SendDataTask, () => _statsService.CollectAndSendStatsData(), Cron.Daily, TimeZoneInfo.Local);
        }

        public void CancelStatsTasks()
        {
            _logger.LogDebug("Cancelling/Removing StatsTasks");

            RecurringJob.RemoveIfExists(SendDataTask);
        }

        public void RunStatCollection()
        {
            _logger.LogInformation("Enqueuing stat collection");
            BackgroundJob.Enqueue(() => _statsService.CollectAndSendStatsData());
        }

        #endregion

        #region UpdateTasks

        public void ScheduleUpdaterTasks()
        {
            _logger.LogInformation("Scheduling Auto-Update tasks");
            RecurringJob.AddOrUpdate("check-updates", () => CheckForUpdate(), Cron.Weekly, TimeZoneInfo.Local);

        }
        #endregion

        public void ScanLibrary(int libraryId, bool forceUpdate = false)
        {
            _logger.LogInformation("Enqueuing library scan for: {LibraryId}", libraryId);
            BackgroundJob.Enqueue(() => _scannerService.ScanLibrary(libraryId));
            // When we do a scan, force cache to re-unpack in case page numbers change
            BackgroundJob.Enqueue(() => _cleanupService.CleanupCacheDirectory());
        }

        public void CleanupChapters(int[] chapterIds)
        {
            BackgroundJob.Enqueue(() => _cacheService.CleanupChapters(chapterIds));
        }

        public void RefreshMetadata(int libraryId, bool forceUpdate = true)
        {
            _logger.LogInformation("Enqueuing library metadata refresh for: {LibraryId}", libraryId);
            BackgroundJob.Enqueue(() => _metadataService.RefreshMetadata(libraryId, forceUpdate));
        }

        public void CleanupTemp()
        {
            var tempDirectory = Path.Join(Directory.GetCurrentDirectory(), "temp");
            BackgroundJob.Enqueue(() => DirectoryService.ClearDirectory(tempDirectory));
        }

        public void RefreshSeriesMetadata(int libraryId, int seriesId, bool forceUpdate = true)
        {
            _logger.LogInformation("Enqueuing series metadata refresh for: {SeriesId}", seriesId);
            BackgroundJob.Enqueue(() => _metadataService.RefreshMetadataForSeries(libraryId, seriesId, forceUpdate));
        }

        public void ScanSeries(int libraryId, int seriesId, bool forceUpdate = false)
        {
            _logger.LogInformation("Enqueuing series scan for: {SeriesId}", seriesId);
            BackgroundJob.Enqueue(() => _scannerService.ScanSeries(libraryId, seriesId, CancellationToken.None));
        }

        public void BackupDatabase()
        {
            BackgroundJob.Enqueue(() => _backupService.BackupDatabase());
        }

        /// <summary>
        /// Not an external call. Only public so that we can call this for a Task
        /// </summary>
        // ReSharper disable once MemberCanBePrivate.Global
        public async Task CheckForUpdate()
        {
            var update = await _versionUpdaterService.CheckForUpdate();
            await _versionUpdaterService.PushUpdate(update);
        }
    }
}
