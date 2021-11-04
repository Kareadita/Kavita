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
using Hangfire;
using Kavita.Common.EnvironmentInfo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks
{
    public class BackupService : IBackupService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<BackupService> _logger;
        private readonly IDirectoryService _directoryService;
        private readonly string _tempDirectory = DirectoryService.TempDirectory;
        private readonly string _logDirectory = DirectoryService.LogDirectory;

        private readonly IList<string> _backupFiles;

        public BackupService(IUnitOfWork unitOfWork, ILogger<BackupService> logger, IDirectoryService directoryService, IConfiguration config)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _directoryService = directoryService;

            var maxRollingFiles = config.GetMaxRollingFiles();
            var loggingSection = config.GetLoggingFileName();
            var files = LogFiles(maxRollingFiles, loggingSection);

            if (new OsInfo(Array.Empty<IOsVersionAdapter>()).IsDocker)
            {
                _backupFiles = new List<string>()
                {
                    "data/appsettings.json",
                    "data/Hangfire.db",
                    "data/Hangfire-log.db",
                    "data/kavita.db",
                    "data/kavita.db-shm", // This wont always be there
                    "data/kavita.db-wal" // This wont always be there
                };
            }
            else
            {
                _backupFiles = new List<string>()
                {
                    "appsettings.json",
                    "Hangfire.db",
                    "Hangfire-log.db",
                    "kavita.db",
                    "kavita.db-shm", // This wont always be there
                    "kavita.db-wal" // This wont always be there
                };
            }

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
                ? DirectoryService.GetFiles(_logDirectory, $@"{Path.GetFileNameWithoutExtension(fi.Name)}{multipleFileRegex}\.log")
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
                _logger.LogError("Could not write to {BackupDirectory}; aborting backup", backupDirectory);
                return;
            }

            var dateString = DateTime.Now.ToShortDateString().Replace("/", "_");
            var zipPath = Path.Join(backupDirectory, $"kavita_backup_{dateString}.zip");

            if (File.Exists(zipPath))
            {
                _logger.LogInformation("{ZipFile} already exists, aborting", zipPath);
                return;
            }

            var tempDirectory = Path.Join(_tempDirectory, dateString);
            DirectoryService.ExistOrCreate(tempDirectory);
            DirectoryService.ClearDirectory(tempDirectory);

            _directoryService.CopyFilesToDirectory(
                _backupFiles.Select(file => Path.Join(Directory.GetCurrentDirectory(), file)).ToList(), tempDirectory);

            await CopyCoverImagesToBackupDirectory(tempDirectory);

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
