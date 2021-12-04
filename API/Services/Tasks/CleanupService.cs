using System;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Entities.Enums;
using API.SignalR;
using Hangfire;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks
{
    public interface ICleanupService
    {
        Task Cleanup();
        void CleanupCacheDirectory();
        Task DeleteSeriesCoverImages();
        Task DeleteChapterCoverImages();
        Task DeleteTagCoverImages();
        Task CleanupBackups();
    }
    /// <summary>
    /// Cleans up after operations on reoccurring basis
    /// </summary>
    public class CleanupService : ICleanupService
    {
        private readonly ILogger<CleanupService> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHubContext<MessageHub> _messageHub;
        private readonly IDirectoryService _directoryService;

        public CleanupService(ILogger<CleanupService> logger,
            IUnitOfWork unitOfWork, IHubContext<MessageHub> messageHub,
            IDirectoryService directoryService)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _messageHub = messageHub;
            _directoryService = directoryService;
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
            _directoryService.ClearDirectory(DirectoryService.TempDirectory);
            await SendProgress(0.1F);
            CleanupCacheDirectory();
            await SendProgress(0.25F);
            _logger.LogInformation("Cleaning old database backups");
            await CleanupBackups();
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

        /// <summary>
        /// Removes all series images that are not in the database. They must follow <see cref="ImageService.SeriesCoverImageRegex"/> filename pattern.
        /// </summary>
        public async Task DeleteSeriesCoverImages()
        {
            var images = await _unitOfWork.SeriesRepository.GetAllCoverImagesAsync();
            var files = _directoryService.GetFiles(_directoryService.CoverImageDirectory, ImageService.SeriesCoverImageRegex);
            foreach (var file in files)
            {
                if (images.Contains(_directoryService.FileSystem.Path.GetFileName(file))) continue;
                _directoryService.FileSystem.File.Delete(file);

            }
        }

        /// <summary>
        /// Removes all chapter/volume images that are not in the database. They must follow <see cref="ImageService.ChapterCoverImageRegex"/> filename pattern.
        /// </summary>
        public async Task DeleteChapterCoverImages()
        {
            var images = await _unitOfWork.ChapterRepository.GetAllCoverImagesAsync();
            var files = _directoryService.GetFiles(_directoryService.CoverImageDirectory, ImageService.ChapterCoverImageRegex);
            foreach (var file in files)
            {
                if (images.Contains(_directoryService.FileSystem.Path.GetFileName(file))) continue;
                _directoryService.FileSystem.File.Delete(file);

            }
        }

        /// <summary>
        /// Removes all collection tag images that are not in the database. They must follow <see cref="ImageService.CollectionTagCoverImageRegex"/> filename pattern.
        /// </summary>
        public async Task DeleteTagCoverImages()
        {
            var images = await _unitOfWork.CollectionTagRepository.GetAllCoverImagesAsync();
            var files = _directoryService.GetFiles(_directoryService.CoverImageDirectory, ImageService.CollectionTagCoverImageRegex);

            // TODO: This is used in 3 different places in this file, refactor into a DirectoryService method
            foreach (var file in files)
            {
                if (images.Contains(_directoryService.FileSystem.Path.GetFileName(file))) continue;
                _directoryService.FileSystem.File.Delete(file);

            }
        }

        /// <summary>
        /// Removes all files and directories in the cache directory
        /// </summary>
        public void CleanupCacheDirectory()
        {
            _logger.LogInformation("Performing cleanup of Cache directory");
            _directoryService.ExistOrCreate(_directoryService.CacheDirectory);

            try
            {
                _directoryService.ClearDirectory(_directoryService.CacheDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an issue deleting one or more folders/files during cleanup");
            }

            _logger.LogInformation("Cache directory purged");
        }

        /// <summary>
        /// Removes Database backups older than 30 days. If all backups are older than 30 days, the latest is kept.
        /// </summary>
        public async Task CleanupBackups()
        {
            const int dayThreshold = 30;
            _logger.LogInformation("Beginning cleanup of Database backups at {Time}", DateTime.Now);
            var backupDirectory =
                (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BackupDirectory)).Value;
            if (!_directoryService.Exists(backupDirectory)) return;

            var deltaTime = DateTime.Today.Subtract(TimeSpan.FromDays(dayThreshold));
            var allBackups = _directoryService.GetFiles(backupDirectory).ToList();
            var expiredBackups = allBackups.Select(filename => _directoryService.FileSystem.FileInfo.FromFileName(filename))
                .Where(f => f.CreationTime < deltaTime)
                .ToList();

            if (expiredBackups.Count == allBackups.Count)
            {
                _logger.LogInformation("All expired backups are older than {Threshold} days. Removing all but last backup", dayThreshold);
                var toDelete = expiredBackups.OrderByDescending(f => f.CreationTime).ToList();
                _directoryService.DeleteFiles(toDelete.Take(toDelete.Count - 1).Select(f => f.FullName));
            }
            else
            {
                _directoryService.DeleteFiles(expiredBackups.Select(f => f.FullName));
            }
            _logger.LogInformation("Finished cleanup of Database backups at {Time}", DateTime.Now);
        }
    }
}
