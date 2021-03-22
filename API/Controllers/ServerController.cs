using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using API.Extensions;
using API.Interfaces;
using API.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace API.Controllers
{
    [Authorize(Policy = "RequireAdminRole")]
    public class ServerController : BaseApiController
    {
        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly ILogger<ServerController> _logger;
        private readonly IConfiguration _config;
        private readonly IDirectoryService _directoryService;
        private readonly IBackupService _backupService;

        public ServerController(IHostApplicationLifetime applicationLifetime, ILogger<ServerController> logger, IConfiguration config,
            IDirectoryService directoryService, IBackupService backupService)
        {
            _applicationLifetime = applicationLifetime;
            _logger = logger;
            _config = config;
            _directoryService = directoryService;
            _backupService = backupService;
        }

        [HttpPost("restart")]
        public ActionResult RestartServer()
        {
            _logger.LogInformation("{UserName} is restarting server from admin dashboard", User.GetUsername());
            
            _applicationLifetime.StopApplication();
            return Ok();
        }

        [HttpGet("logs")]
        public async Task<ActionResult> GetLogs()
        {
            var files = _backupService.LogFiles(_config.GetMaxRollingFiles(), _config.GetLoggingFileName());
            
            var tempDirectory = Path.Join(Directory.GetCurrentDirectory(), "temp");
            var dateString = DateTime.Now.ToShortDateString().Replace("/", "_");
            
            var tempLocation = Path.Join(tempDirectory, "logs_" + dateString);
            DirectoryService.ExistOrCreate(tempLocation);
            if (!_directoryService.CopyFilesToDirectory(files, tempLocation))
            {
                return BadRequest("Unable to copy files to temp directory for log download.");
            }
            
            var zipPath = Path.Join(tempDirectory, $"kavita_logs_{dateString}.zip");
            try
            {
                ZipFile.CreateFromDirectory(tempLocation, zipPath);
            }
            catch (AggregateException ex)
            {
                _logger.LogError(ex, "There was an issue when archiving library backup");
                return BadRequest("There was an issue when archiving library backup");
            }
            var fileBytes = await _directoryService.ReadFileAsync(zipPath);
            
            _directoryService.ClearAndDeleteDirectory(tempLocation);
            (new FileInfo(zipPath)).Delete(); 
            
            return File(fileBytes, "application/zip", Path.GetFileName(zipPath));  
        }
    }
}