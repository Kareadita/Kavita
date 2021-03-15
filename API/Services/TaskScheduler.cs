using System.IO;
using System.Linq;
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
        private readonly IDirectoryService _directoryService;

        public static BackgroundJobServer Client => new BackgroundJobServer();
        // new BackgroundJobServerOptions()
        // {
        //     WorkerCount = 1
        // }

        public TaskScheduler(ICacheService cacheService, ILogger<TaskScheduler> logger, IScannerService scannerService, 
            IUnitOfWork unitOfWork, IMetadataService metadataService, IBackupService backupService, ICleanupService cleanupService,
            IDirectoryService directoryService)
        {
            _cacheService = cacheService;
            _logger = logger;
            _scannerService = scannerService;
            _unitOfWork = unitOfWork;
            _metadataService = metadataService;
            _backupService = backupService;
            _cleanupService = cleanupService;
            _directoryService = directoryService;
            
            ScheduleTasks();
        }

        public void ScheduleTasks()
        {
            _logger.LogInformation("Scheduling reoccurring tasks");

            string setting = null;
            setting = Task.Run(() => _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.TaskScan)).Result.Value;
            if (setting != null)
            {
                _logger.LogDebug("Scheduling Scan Library Task for {Cron}", setting);
                RecurringJob.AddOrUpdate("scan-libraries", () => _scannerService.ScanLibraries(), () => CronConverter.ConvertToCronNotation(setting));
            }
            else
            {
                RecurringJob.AddOrUpdate("scan-libraries", () => _scannerService.ScanLibraries(), Cron.Daily);
            }
            
            setting = Task.Run(() => _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.TaskBackup)).Result.Value;
            if (setting != null)
            {
                _logger.LogDebug("Scheduling Backup Task for {Cron}", setting);
                RecurringJob.AddOrUpdate("backup", () => _backupService.BackupDatabase(), () => CronConverter.ConvertToCronNotation(setting));
            }
            else
            {
                RecurringJob.AddOrUpdate("backup", () => _backupService.BackupDatabase(), Cron.Weekly);
            }
            
            RecurringJob.AddOrUpdate("cleanup", () => _cleanupService.Cleanup(), Cron.Daily);
        }

        public void ScanLibrary(int libraryId, bool forceUpdate = false)
        {
            
            _logger.LogInformation("Enqueuing library scan for: {LibraryId}", libraryId);
            BackgroundJob.Enqueue(() => _scannerService.ScanLibrary(libraryId, forceUpdate));
            //BackgroundJob.Enqueue(() => _cleanupService.Cleanup()); // When we do a scan, force cache to re-unpack in case page numbers change
            RecurringJob.Trigger("cleanup"); // TODO: Alternate way to trigger jobs. Test this out and see if we should switch.
        }

        public void CleanupChapters(int[] chapterIds)
        {
            BackgroundJob.Enqueue(() => _cacheService.CleanupChapters(chapterIds));
        }

        public void RefreshMetadata(int libraryId, bool forceUpdate = true)
        {
            _logger.LogInformation("Enqueuing library metadata refresh for: {LibraryId}", libraryId);
            BackgroundJob.Enqueue((() => _metadataService.RefreshMetadata(libraryId, forceUpdate)));
        }

        public void CleanupTemp()
        {
            var tempDirectory = Path.Join(Directory.GetCurrentDirectory(), "temp");
            BackgroundJob.Enqueue((() => _directoryService.ClearDirectory(tempDirectory)));
        }

        public void BackupDatabase()
        {
            BackgroundJob.Enqueue(() => _backupService.BackupDatabase());
        }
    }
}