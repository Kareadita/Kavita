using System;
using System.IO;
using System.Threading.Tasks;
using API.Extensions;
using API.Interfaces.Services;
using Kavita.Common;
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
        private readonly IBackupService _backupService;
        private readonly IArchiveService _archiveService;

        public ServerController(IHostApplicationLifetime applicationLifetime, ILogger<ServerController> logger, IConfiguration config,
            IBackupService backupService, IArchiveService archiveService)
        {
            _applicationLifetime = applicationLifetime;
            _logger = logger;
            _config = config;
            _backupService = backupService;
            _archiveService = archiveService;
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
            try
            {
                var (fileBytes, zipPath) = await _archiveService.CreateZipForDownload(files, "logs");
                return File(fileBytes, "application/zip", Path.GetFileName(zipPath));  
            }
            catch (KavitaException ex)
            {
                return BadRequest(ex.Message);
            }
        }
        
        
    }
}