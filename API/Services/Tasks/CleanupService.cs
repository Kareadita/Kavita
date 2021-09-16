using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Interfaces;
using API.Interfaces.Services;
using Hangfire;
using Microsoft.Extensions.Logging;
using NetVips;

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
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDirectoryService _directoryService;

        public CleanupService(ICacheService cacheService, ILogger<CleanupService> logger,
            IBackupService backupService, IUnitOfWork unitOfWork, IDirectoryService directoryService)
        {
            _cacheService = cacheService;
            _logger = logger;
            _backupService = backupService;
            _unitOfWork = unitOfWork;
            _directoryService = directoryService;
        }

        /// <summary>
        /// Cleans up Temp, cache, deleted cover images,  and old database backups
        /// </summary>
        [AutomaticRetry(Attempts = 3, LogEvents = false, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
        public async Task Cleanup()
        {
            _logger.LogInformation("Starting Cleanup");
            _logger.LogInformation("Cleaning temp directory");
            var tempDirectory = Path.Join(Directory.GetCurrentDirectory(), "temp");
            DirectoryService.ClearDirectory(tempDirectory);
            _logger.LogInformation("Cleaning cache directory");
            _cacheService.Cleanup();
            _logger.LogInformation("Cleaning old database backups");
            _backupService.CleanupBackups();
            _logger.LogInformation("Cleaning deleted cover images");
            await DeleteSeriesCoverImages();
            await DeleteChapterCoverImages();
            _logger.LogInformation("Cleanup finished");
        }

        private async Task DeleteSeriesCoverImages()
        {
            var images = await _unitOfWork.SeriesRepository.GetAllCoverImagesAsync();
            var files = _directoryService.GetFiles(DirectoryService.CoverImageDirectory, @"seres\d+");
            foreach (var file in files)
            {
                if (images.Contains(file)) continue;
                File.Delete(file);

            }
        }

        private async Task DeleteChapterCoverImages()
        {
            var images = await _unitOfWork.ChapterRepository.GetAllCoverImagesAsync();
            var files = _directoryService.GetFiles(DirectoryService.CoverImageDirectory, @"v\d+_c\d+");
            foreach (var file in files)
            {
                if (images.Contains(file)) continue;
                File.Delete(file);

            }
        }
    }
}
