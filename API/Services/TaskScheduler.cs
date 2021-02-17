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

        public BackgroundJobServer Client => new BackgroundJobServer();
        // new BackgroundJobServerOptions()
        // {
        //     WorkerCount = 1
        // }

        public TaskScheduler(ICacheService cacheService, ILogger<TaskScheduler> logger, IScannerService scannerService, 
            IUnitOfWork unitOfWork, IMetadataService metadataService, IBackupService backupService)
        {
            _cacheService = cacheService;
            _logger = logger;
            _scannerService = scannerService;
            _unitOfWork = unitOfWork;
            _metadataService = metadataService;
            _backupService = backupService;


            ScheduleTasks();
            //JobStorage.Current.GetMonitoringApi().

        }

        public void ScheduleTasks()
        {
            _logger.LogInformation("Scheduling reoccurring tasks");
            string setting = null;
            setting = Task.Run(() => _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.TaskScan)).Result.Value;
            if (setting != null)
            {
                _logger.LogDebug("Scheduling Scan Library Task for {Cron}", setting);
                RecurringJob.AddOrUpdate(() => _scannerService.ScanLibraries(), () => CronConverter.ConvertToCronNotation(setting));
            }
            else
            {
                RecurringJob.AddOrUpdate(() => _scannerService.ScanLibraries(), Cron.Daily);
            }
            
            setting = Task.Run(() => _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.TaskBackup)).Result.Value;
            if (setting != null)
            {
                _logger.LogDebug("Scheduling Backup Task for {Cron}", setting);
                RecurringJob.AddOrUpdate(() => _backupService.BackupDatabase(), () => CronConverter.ConvertToCronNotation(setting2));
            }
            else
            {
                RecurringJob.AddOrUpdate(() => _backupService.BackupDatabase(), Cron.Weekly);
            }
            
            RecurringJob.AddOrUpdate(() => _cacheService.Cleanup(), Cron.Daily);
        }

        public void ScanLibrary(int libraryId, bool forceUpdate = false)
        {
            _logger.LogInformation("Enqueuing library scan for: {LibraryId}", libraryId);
            BackgroundJob.Enqueue(() => _scannerService.ScanLibrary(libraryId, forceUpdate));
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

        public void BackupDatabase()
        {
            BackgroundJob.Enqueue(() => _backupService.BackupDatabase());
        }
    }
}