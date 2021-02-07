using System.Threading.Tasks;
using API.Entities.Enums;
using API.Helpers.Converters;
using API.Interfaces;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace API.Services
{
    public class TaskScheduler : ITaskScheduler
    {
        private readonly ICacheService _cacheService;
        private readonly ILogger<TaskScheduler> _logger;
        private readonly IScannerService _scannerService;
        public BackgroundJobServer Client => new BackgroundJobServer();

        public TaskScheduler(ICacheService cacheService, ILogger<TaskScheduler> logger, IScannerService scannerService, IUnitOfWork unitOfWork)
        {
            _cacheService = cacheService;
            _logger = logger;
            _scannerService = scannerService;

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

        public void ScanSeries(int libraryId, int seriesId)
        {
            _logger.LogInformation($"Enqueuing series scan for series: {seriesId}");
            BackgroundJob.Enqueue(() => _scannerService.ScanSeries(libraryId, seriesId));
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
        
    }
}