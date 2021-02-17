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
            
            var fileInfos = _backupFiles.Select(file => new FileInfo(Path.Join(Directory.GetCurrentDirectory(), file))).ToList();

            var zipPath = Path.Join(backupDirectory, $"kavita_backup_{DateTime.Now}.zip");
            using (var zipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (var fileInfo in fileInfos)
                {
                    zipArchive.CreateEntryFromFile(fileInfo.FullName,  fileInfo.Name);
                }
            }
            
            _logger.LogInformation("Database backup completed");
            throw new System.NotImplementedException();
        }
    }
}