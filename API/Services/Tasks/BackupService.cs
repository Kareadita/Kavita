using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.Entities.Enums;
using API.Extensions;
using API.Interfaces;
using API.Interfaces.Services;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks
{
    public class BackupService : IBackupService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<BackupService> _logger;
        private readonly IDirectoryService _directoryService;
        private readonly string _tempDirectory = Path.Join(Directory.GetCurrentDirectory(), "temp");

        private readonly IList<string> _backupFiles;

        public BackupService(IUnitOfWork unitOfWork, ILogger<BackupService> logger, IDirectoryService directoryService, IConfiguration config)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _directoryService = directoryService;
            
            var maxRollingFiles = config.GetMaxRollingFiles();
            var loggingSection = config.GetLoggingFileName();
            var files = LogFiles(maxRollingFiles, loggingSection);
            _backupFiles = new List<string>()
            {
                "appsettings.json",
                "Hangfire.db",
                "Hangfire-log.db",
                "kavita.db",
                "kavita.db-shm", // This wont always be there
                "kavita.db-wal", // This wont always be there
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
                ? _directoryService.GetFiles(Directory.GetCurrentDirectory(), $@"{fi.Name}{multipleFileRegex}\.log")
                : new string[] {"kavita.log"};
            return files;
        }

        [AutomaticRetry(Attempts = 3, LogEvents = false, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
        public void BackupDatabase()
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
            _directoryService.ClearDirectory(tempDirectory);
            
            _directoryService.CopyFilesToDirectory(
                _backupFiles.Select(file => Path.Join(Directory.GetCurrentDirectory(), file)).ToList(), tempDirectory);
            try
            {
                ZipFile.CreateFromDirectory(tempDirectory, zipPath);
            }
            catch (AggregateException ex)
            {
                _logger.LogError(ex, "There was an issue when archiving library backup");
            }

            _directoryService.ClearAndDeleteDirectory(tempDirectory);
            _logger.LogInformation("Database backup completed");
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
            var allBackups = _directoryService.GetFiles(backupDirectory).ToList();
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