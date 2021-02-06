using API.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace API.Controllers
{
    [Authorize(Policy = "RequireAdminRole")]
    public class ServerController : BaseApiController
    {
        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly ILogger<ServerController> _logger;

        public ServerController(IHostApplicationLifetime applicationLifetime, ILogger<ServerController> logger)
        {
            _applicationLifetime = applicationLifetime;
            _logger = logger;
        }

        [HttpPost]
        public ActionResult RestartServer()
        {
            _logger.LogInformation($"{User.GetUsername()} is restarting server from admin dashboard.");
            
            _applicationLifetime.StopApplication();
            return Ok();
        }
    }
}