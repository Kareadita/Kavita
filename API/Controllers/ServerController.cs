using System;
using System.IO;
using System.Threading.Tasks;
using API.Extensions;
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

        public ServerController(IHostApplicationLifetime applicationLifetime, ILogger<ServerController> logger, IConfiguration config,
            IDirectoryService directoryService)
        {
            _applicationLifetime = applicationLifetime;
            _logger = logger;
            _config = config;
            _directoryService = directoryService;
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
            // TODO: Zip up the log files
            var maxRollingFiles = int.Parse(_config.GetSection("Logging").GetSection("File").GetSection("MaxRollingFiles").Value);
            var loggingSection = _config.GetSection("Logging").GetSection("File").GetSection("Path").Value;

            var multipleFileRegex = maxRollingFiles > 0 ? @"\d*" : string.Empty;
            FileInfo fi = new FileInfo(loggingSection);

            var files = _directoryService.GetFilesWithExtension(Directory.GetCurrentDirectory(), $@"{fi.Name}{multipleFileRegex}\.log");
            Console.WriteLine(files);
            
            var logFile = Path.Join(Directory.GetCurrentDirectory(), loggingSection);
            _logger.LogInformation("Fetching download of logs: {LogFile}", logFile);
            
            // First, copy the file to temp
            
            var originalFile = new FileInfo(logFile);
            var tempDirectory = Path.Join(Directory.GetCurrentDirectory(), "temp");
            _directoryService.ExistOrCreate(tempDirectory);
            var tempLocation = Path.Join(tempDirectory, originalFile.Name);
            originalFile.CopyTo(tempLocation); // TODO: Make this unique based on date
            
            // Read into memory
            await using var memory = new MemoryStream();
            // We need to copy it else it will throw an exception
            await using (var stream = new FileStream(tempLocation, FileMode.Open, FileAccess.Read))  
            {  
                await stream.CopyToAsync(memory);  
            }
            memory.Position = 0;
            
            // Delete temp
            (new FileInfo(tempLocation)).Delete();
            
            return File(memory, "text/plain", Path.GetFileName(logFile));  
        }
    }
}