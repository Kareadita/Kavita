using API.Interfaces;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace API.Services
{
    public class TaskScheduler : ITaskScheduler
    {
        private readonly ILogger<TaskScheduler> _logger;
        private readonly BackgroundJobServer _client;

        public TaskScheduler(ICacheService cacheService, ILogger<TaskScheduler> logger)
        {
            _logger = logger;
            _client = new BackgroundJobServer();
            
            _logger.LogInformation("Scheduling/Updating cache cleanup on a daily basis.");
            RecurringJob.AddOrUpdate(() => cacheService.Cleanup(), Cron.Daily);
            //RecurringJob.AddOrUpdate(() => scanService.ScanLibraries(), Cron.Daily);
        }
        
        
    }
}