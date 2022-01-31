using System;
using System.IO;
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
        Task CleanupDbEntries();
        void CleanupCacheDirectory();
        Task DeleteSeriesCoverImages();
        Task DeleteChapterCoverImages();
        Task DeleteTagCoverImages();
        Task CleanupBackups();
        Task CleanupBookmarks();
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
            _directoryService.ClearDirectory(_directoryService.TempDirectory);
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
            await SendProgress(0.8F);
            _logger.LogInformation("Cleaning old bookmarks");
            await CleanupBookmarks();
            await SendProgress(1F);
            _logger.LogInformation("Cleanup finished");
        }

        /// <summary>
        /// Cleans up abandon rows in the DB
        /// </summary>
        public async Task CleanupDbEntries()
        {
            await _unitOfWork.AppUserProgressRepository.CleanupAbandonedChapters();
            await _unitOfWork.PersonRepository.RemoveAllPeopleNoLongerAssociated();
            await _unitOfWork.GenreRepository.RemoveAllGenreNoLongerAssociated();
            await _unitOfWork.CollectionTagRepository.RemoveTagsWithoutSeries();
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
            _directoryService.DeleteFiles(files.Where(file => !images.Contains(_directoryService.FileSystem.Path.GetFileName(file))));
        }

        /// <summary>
        /// Removes all chapter/volume images that are not in the database. They must follow <see cref="ImageService.ChapterCoverImageRegex"/> filename pattern.
        /// </summary>
        public async Task DeleteChapterCoverImages()
        {
            var images = await _unitOfWork.ChapterRepository.GetAllCoverImagesAsync();
            var files = _directoryService.GetFiles(_directoryService.CoverImageDirectory, ImageService.ChapterCoverImageRegex);
            _directoryService.DeleteFiles(files.Where(file => !images.Contains(_directoryService.FileSystem.Path.GetFileName(file))));
        }

        /// <summary>
        /// Removes all collection tag images that are not in the database. They must follow <see cref="ImageService.CollectionTagCoverImageRegex"/> filename pattern.
        /// </summary>
        public async Task DeleteTagCoverImages()
        {
            var images = await _unitOfWork.CollectionTagRepository.GetAllCoverImagesAsync();
            var files = _directoryService.GetFiles(_directoryService.CoverImageDirectory, ImageService.CollectionTagCoverImageRegex);
            _directoryService.DeleteFiles(files.Where(file => !images.Contains(_directoryService.FileSystem.Path.GetFileName(file))));
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

        /// <summary>
        /// Removes all files in the BookmarkDirectory that don't currently have bookmarks in the Database
        /// </summary>
        public async Task CleanupBookmarks()
        {
            // Search all files in bookmarks/ except bookmark files and delete those
            var bookmarkDirectory =
                (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BookmarkDirectory)).Value;
            var allBookmarkFiles = _directoryService.GetFiles(bookmarkDirectory, searchOption: SearchOption.AllDirectories).Select(Parser.Parser.NormalizePath);
            var bookmarks = (await _unitOfWork.UserRepository.GetAllBookmarksAsync())
                .Select(b => Parser.Parser.NormalizePath(_directoryService.FileSystem.Path.Join(bookmarkDirectory,
                    b.FileName)));


            var filesToDelete = allBookmarkFiles.ToList().Except(bookmarks).ToList();
            _logger.LogDebug("[Bookmarks] Bookmark cleanup wants to delete {Count} files", filesToDelete.Count());

            _directoryService.DeleteFiles(filesToDelete);

            // Clear all empty directories
            foreach (var directory in _directoryService.FileSystem.Directory.GetDirectories(bookmarkDirectory, "", SearchOption.AllDirectories))
            {
                if (_directoryService.FileSystem.Directory.GetFiles(directory, "", SearchOption.AllDirectories).Length == 0 &&
                    _directoryService.FileSystem.Directory.GetDirectories(directory).Length == 0)
                {
                    _directoryService.FileSystem.Directory.Delete(directory, false);
                }
            }


        }
    }
}
