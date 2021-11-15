using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using API.Entities.Enums;
using API.Extensions;
using API.Interfaces;
using API.Interfaces.Services;
using API.SignalR;
using Hangfire;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks
{
    public class BackupService : IBackupService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<BackupService> _logger;
        private readonly IDirectoryService _directoryService;
        private readonly IHubContext<MessageHub> _messageHub;

        private readonly IList<string> _backupFiles;

        public BackupService(IUnitOfWork unitOfWork, ILogger<BackupService> logger,
            IDirectoryService directoryService, IConfiguration config, IHubContext<MessageHub> messageHub)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _directoryService = directoryService;
            _messageHub = messageHub;

            var maxRollingFiles = config.GetMaxRollingFiles();
            var loggingSection = config.GetLoggingFileName();
            var files = LogFiles(maxRollingFiles, loggingSection);


            _backupFiles = new List<string>()
            {
                "appsettings.json",
                "Hangfire.db", // This is not used atm
                "Hangfire-log.db", // This is not used atm
                "kavita.db",
                "kavita.db-shm", // This wont always be there
                "kavita.db-wal" // This wont always be there
            };

            foreach (var file in files.Select(f => (new FileInfo(f)).Name).ToList())
            {
                _backupFiles.Add(file);
            }
        }

        public IEnumerable<string> LogFiles(int maxRollingFiles, string logFileName)
        {
            var multipleFileRegex = maxRollingFiles > 0 ? @"\d*" : string.Empty;
            var fi = new FileInfo(logFileName);

            var files = maxRollingFiles > 0
                ? DirectoryService.GetFiles(DirectoryService.LogDirectory, $@"{Path.GetFileNameWithoutExtension(fi.Name)}{multipleFileRegex}\.log")
                : new[] {"kavita.log"};
            return files;
        }

        /// <summary>
        /// Will backup anything that needs to be backed up. This includes logs, setting files, bare minimum cover images (just locked and first cover).
        /// </summary>
        [AutomaticRetry(Attempts = 3, LogEvents = false, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
        public async Task BackupDatabase()
        {
            _logger.LogInformation("Beginning backup of Database at {BackupTime}", DateTime.Now);
            var backupDirectory = Task.Run(() => _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BackupDirectory)).Result.Value;

            _logger.LogDebug("Backing up to {BackupDirectory}", backupDirectory);
            if (!DirectoryService.ExistOrCreate(backupDirectory))
            {
                _logger.LogCritical("Could not write to {BackupDirectory}; aborting backup", backupDirectory);
                return;
            }

            await SendProgress(0F);

            var dateString = $"{DateTime.Now.ToShortDateString()}_{DateTime.Now.ToLongTimeString()}".Replace("/", "_").Replace(":", "_");
            var zipPath = Path.Join(backupDirectory, $"kavita_backup_{dateString}.zip");

            if (File.Exists(zipPath))
            {
                _logger.LogInformation("{ZipFile} already exists, aborting", zipPath);
                return;
            }

            var tempDirectory = Path.Join(DirectoryService.TempDirectory, dateString);
            DirectoryService.ExistOrCreate(tempDirectory);
            DirectoryService.ClearDirectory(tempDirectory);

            _directoryService.CopyFilesToDirectory(
                _backupFiles.Select(file => Path.Join(DirectoryService.ConfigDirectory, file)).ToList(), tempDirectory);

            await SendProgress(0.25F);

            await CopyCoverImagesToBackupDirectory(tempDirectory);

            await SendProgress(0.75F);

            try
            {
                ZipFile.CreateFromDirectory(tempDirectory, zipPath);
            }
            catch (AggregateException ex)
            {
                _logger.LogError(ex, "There was an issue when archiving library backup");
            }

            DirectoryService.ClearAndDeleteDirectory(tempDirectory);
            _logger.LogInformation("Database backup completed");
            await SendProgress(1F);
        }

        private async Task CopyCoverImagesToBackupDirectory(string tempDirectory)
        {
            var outputTempDir = Path.Join(tempDirectory, "covers");
            DirectoryService.ExistOrCreate(outputTempDir);

            try
            {
                var seriesImages = await _unitOfWork.SeriesRepository.GetLockedCoverImagesAsync();
                _directoryService.CopyFilesToDirectory(
                    seriesImages.Select(s => Path.Join(DirectoryService.CoverImageDirectory, s)), outputTempDir);

                var collectionTags = await _unitOfWork.CollectionTagRepository.GetAllCoverImagesAsync();
                _directoryService.CopyFilesToDirectory(
                    collectionTags.Select(s => Path.Join(DirectoryService.CoverImageDirectory, s)), outputTempDir);

                var chapterImages = await _unitOfWork.ChapterRepository.GetCoverImagesForLockedChaptersAsync();
                _directoryService.CopyFilesToDirectory(
                    chapterImages.Select(s => Path.Join(DirectoryService.CoverImageDirectory, s)), outputTempDir);
            }
            catch (IOException)
            {
                // Swallow exception. This can be a duplicate cover being copied as chapter and volumes can share same file.
            }

            if (!DirectoryService.GetFiles(outputTempDir).Any())
            {
                DirectoryService.ClearAndDeleteDirectory(outputTempDir);
            }
        }

        private async Task SendProgress(float progress)
        {
            await _messageHub.Clients.All.SendAsync(SignalREvents.BackupDatabaseProgress,
                MessageFactory.BackupDatabaseProgressEvent(progress));
        }

        /// <summary>
        /// Removes Database backups older than 30 days. If all backups are older than 30 days, the latest is kept.
        /// </summary>
        public void CleanupBackups()
        {
            const int dayThreshold = 30;
            _logger.LogInformation("Beginning cleanup of Database backups at {Time}", DateTime.Now);
            var backupDirectory = Task.Run(() => _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BackupDirectory)).Result.Value;
            if (!_directoryService.Exists(backupDirectory)) return;
            var deltaTime = DateTime.Today.Subtract(TimeSpan.FromDays(dayThreshold));
            var allBackups = DirectoryService.GetFiles(backupDirectory).ToList();
            var expiredBackups = allBackups.Select(filename => new FileInfo(filename))
                .Where(f => f.CreationTime > deltaTime)
                .ToList();
            if (expiredBackups.Count == allBackups.Count)
            {
                _logger.LogInformation("All expired backups are older than {Threshold} days. Removing all but last backup", dayThreshold);
                var toDelete = expiredBackups.OrderByDescending(f => f.CreationTime).ToList();
                for (var i = 1; i < toDelete.Count; i++)
                {
                    try
                    {
                        toDelete[i].Delete();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "There was an issue deleting {FileName}", toDelete[i].Name);
                    }
                }
            }
            else
            {
                foreach (var file in expiredBackups)
                {
                    try
                    {
                        file.Delete();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "There was an issue deleting {FileName}", file.Name);
                    }
                }

            }
            _logger.LogInformation("Finished cleanup of Database backups at {Time}", DateTime.Now);
        }

    }
}
