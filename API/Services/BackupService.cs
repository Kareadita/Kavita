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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace API.Services
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

            var multipleFileRegex = maxRollingFiles > 0 ? @"\d*" : string.Empty;
            var fi = new FileInfo(loggingSection);
            

            var files = maxRollingFiles > 0
                ? _directoryService.GetFiles(Directory.GetCurrentDirectory(), $@"{fi.Name}{multipleFileRegex}\.log")
                : new string[] {"kavita.log"};
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

        public void BackupDatabase()
        {
            _logger.LogInformation("Beginning backup of Database at {BackupTime}", DateTime.Now);
            var backupDirectory = Task.Run(() => _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BackupDirectory)).Result.Value;
            
            _logger.LogDebug("Backing up to {BackupDirectory}", backupDirectory);
            if (!_directoryService.ExistOrCreate(backupDirectory))
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
            _directoryService.ExistOrCreate(tempDirectory);
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
        
    }
}