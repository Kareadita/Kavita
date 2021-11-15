using System.IO;
using System.Threading.Tasks;
using API.Interfaces;
using API.Interfaces.Services;
using API.SignalR;
using Hangfire;
using Microsoft.AspNetCore.SignalR;
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
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHubContext<MessageHub> _messageHub;

        public CleanupService(ICacheService cacheService, ILogger<CleanupService> logger,
            IBackupService backupService, IUnitOfWork unitOfWork, IHubContext<MessageHub> messageHub)
        {
            _cacheService = cacheService;
            _logger = logger;
            _backupService = backupService;
            _unitOfWork = unitOfWork;
            _messageHub = messageHub;
        }

        public void CleanupCacheDirectory()
        {
            _logger.LogInformation("Cleaning cache directory");
            _cacheService.Cleanup();
        }

        /// <summary>
        /// Cleans up Temp, cache, deleted cover images,  and old database backups
        /// </summary>
        [AutomaticRetry(Attempts = 3, LogEvents = false, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
        public async Task Cleanup()
        {
            _logger.LogInformation("Starting Cleanup");
            await SendProgress(0F);
            _logger.LogInformation("Cleaning temp directory");
            DirectoryService.ClearDirectory(DirectoryService.TempDirectory);
            await SendProgress(0.1F);
            CleanupCacheDirectory();
            await SendProgress(0.25F);
            _logger.LogInformation("Cleaning old database backups");
            _backupService.CleanupBackups();
            await SendProgress(0.50F);
            _logger.LogInformation("Cleaning deleted cover images");
            await DeleteSeriesCoverImages();
            await SendProgress(0.6F);
            await DeleteChapterCoverImages();
            await SendProgress(0.7F);
            await DeleteTagCoverImages();
            await SendProgress(1F);
            _logger.LogInformation("Cleanup finished");
        }

        private async Task SendProgress(float progress)
        {
            await _messageHub.Clients.All.SendAsync(SignalREvents.CleanupProgress,
                MessageFactory.CleanupProgressEvent(progress));
        }

        private async Task DeleteSeriesCoverImages()
        {
            var images = await _unitOfWork.SeriesRepository.GetAllCoverImagesAsync();
            var files = DirectoryService.GetFiles(DirectoryService.CoverImageDirectory, ImageService.SeriesCoverImageRegex);
            foreach (var file in files)
            {
                if (images.Contains(Path.GetFileName(file))) continue;
                File.Delete(file);

            }
        }

        private async Task DeleteChapterCoverImages()
        {
            var images = await _unitOfWork.ChapterRepository.GetAllCoverImagesAsync();
            var files = DirectoryService.GetFiles(DirectoryService.CoverImageDirectory, ImageService.ChapterCoverImageRegex);
            foreach (var file in files)
            {
                if (images.Contains(Path.GetFileName(file))) continue;
                File.Delete(file);

            }
        }

        private async Task DeleteTagCoverImages()
        {
            var images = await _unitOfWork.CollectionTagRepository.GetAllCoverImagesAsync();
            var files = DirectoryService.GetFiles(DirectoryService.CoverImageDirectory, ImageService.CollectionTagCoverImageRegex);
            foreach (var file in files)
            {
                if (images.Contains(Path.GetFileName(file))) continue;
                File.Delete(file);

            }
        }
    }
}
