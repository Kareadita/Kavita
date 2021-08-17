using System.IO;
using API.Interfaces.Services;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks
{
    /// <summary>
    /// Cleans up after operations on reoccurring basis
    /// </summary>
    public class CleanupService : ICleanupService
    {
        private readonly ICacheService _cacheService;
        private readonly ILogger<CleanupService> _logger;
        private readonly IBackupService _backupService;

        public CleanupService(ICacheService cacheService, ILogger<CleanupService> logger, IBackupService backupService)
        {
            _cacheService = cacheService;
            _logger = logger;
            _backupService = backupService;
        }

        /// <summary>
        /// Cleans up Temp, cache, and old database backups
        /// </summary>
        [AutomaticRetry(Attempts = 3, LogEvents = false, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
        public void Cleanup()
        {
            _logger.LogInformation("Cleaning temp directory");
            var tempDirectory = Path.Join(Directory.GetCurrentDirectory(), "temp");
            DirectoryService.ClearDirectory(tempDirectory);
            _logger.LogInformation("Cleaning cache directory");
            _cacheService.Cleanup();
            _logger.LogInformation("Cleaning old database backups");
            _backupService.CleanupBackups();
        }

        [AutomaticRetry(Attempts = 3, LogEvents = false, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
        public void RemoveAbandonedRows()
        {

        }
    }
}
