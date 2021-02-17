using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using API.Entities.Enums;
using API.Interfaces;
using API.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace API.Services
{
    public class BackupService : IBackupService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<BackupService> _logger;
        private readonly IDirectoryService _directoryService;
        private readonly string _tempDirectory = Path.Join(Directory.GetCurrentDirectory(), "temp");

        private readonly IList<string> _backupFiles = new List<string>()
        {
            "appsettings.json",
            "Hangfire.db",
            "Hangfire-log.db",
            "kavita.db",
            "kavita.db-shm",
            "kavita.db-wal",
            "kavita.log",
        };

        public BackupService(IUnitOfWork unitOfWork, ILogger<BackupService> logger, IDirectoryService directoryService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _directoryService = directoryService;
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


            foreach (var file in _backupFiles)
            {
                var originalFile = new FileInfo(Path.Join(Directory.GetCurrentDirectory(), file));
                originalFile.CopyTo(Path.Join(tempDirectory, originalFile.Name));
            }

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