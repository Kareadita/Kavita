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
        private readonly IMetadataService _metadataService;

        public BackgroundJobServer Client => new BackgroundJobServer(new BackgroundJobServerOptions()
        {
            WorkerCount = 1
        });

        public TaskScheduler(ICacheService cacheService, ILogger<TaskScheduler> logger, IScannerService scannerService, 
            IUnitOfWork unitOfWork, IMetadataService metadataService)
        {
            _cacheService = cacheService;
            _logger = logger;
            _scannerService = scannerService;
            _metadataService = metadataService;

            _logger.LogInformation("Scheduling/Updating cache cleanup on a daily basis.");
            var setting = Task.Run(() => unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.TaskScan)).Result;
            if (setting != null)
            {
                RecurringJob.AddOrUpdate(() => _scannerService.ScanLibraries(), () => CronConverter.ConvertToCronNotation(setting.Value));
            }
            else
            {
                RecurringJob.AddOrUpdate(() => _cacheService.Cleanup(), Cron.Daily);
                RecurringJob.AddOrUpdate(() => _scannerService.ScanLibraries(), Cron.Daily);
            }
            
            //JobStorage.Current.GetMonitoringApi().
            
        }

        public void ScanLibrary(int libraryId, bool forceUpdate = false)
        {
            _logger.LogInformation($"Enqueuing library scan for: {libraryId}");
            BackgroundJob.Enqueue(() => _scannerService.ScanLibrary(libraryId, forceUpdate));
        }

        public void CleanupChapters(int[] chapterIds)
        {
            BackgroundJob.Enqueue(() => _cacheService.CleanupChapters(chapterIds));
            
        }

        public void RefreshMetadata(int libraryId, bool forceUpdate = true)
        {
            _logger.LogInformation($"Enqueuing library metadata refresh for: {libraryId}");
            BackgroundJob.Enqueue((() => _metadataService.RefreshMetadata(libraryId, forceUpdate)));
        }

        public void ScanLibraryInternal(int libraryId, bool forceUpdate)
        {
            _scannerService.ScanLibrary(libraryId, forceUpdate);
            _metadataService.RefreshMetadata(libraryId, forceUpdate);
        } 
        
    }
}