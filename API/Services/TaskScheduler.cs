using System.IO;
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

        public static BackgroundJobServer Client => new BackgroundJobServer();


        public TaskScheduler(ICacheService cacheService, ILogger<TaskScheduler> logger, IScannerService scannerService, 
            IUnitOfWork unitOfWork, IMetadataService metadataService, IBackupService backupService, ICleanupService cleanupService)
        {
            _cacheService = cacheService;
            _logger = logger;
            _scannerService = scannerService;
            _unitOfWork = unitOfWork;
            _metadataService = metadataService;
            _backupService = backupService;
            _cleanupService = cleanupService;
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
                    () => CronConverter.ConvertToCronNotation(scanLibrarySetting));
            }
            else
            {
                RecurringJob.AddOrUpdate("scan-libraries", () => _scannerService.ScanLibraries(), Cron.Daily);
            }
            
            setting = Task.Run(() => _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.TaskBackup)).Result.Value;
            if (setting != null)
            {
                _logger.LogDebug("Scheduling Backup Task for {Setting}", setting);
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
            // When we do a scan, force cache to re-unpack in case page numbers change
            BackgroundJob.Enqueue(() => _cleanupService.Cleanup()); 
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
            BackgroundJob.Enqueue((() => DirectoryService.ClearDirectory(tempDirectory)));
        }

        public void BackupDatabase()
        {
            BackgroundJob.Enqueue(() => _backupService.BackupDatabase());
        }
    }
}