using System;
using System.Threading.Tasks;
using API.DTOs;
using API.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers
{
    public class StatsController : BaseApiController
    {
        private readonly ILogger<StatsController> _logger;
        private readonly IStatsService _statsService;

        public StatsController(ILogger<StatsController> logger, IStatsService statsService)
        {
            _logger = logger;
            _statsService = statsService;
        }

        [AllowAnonymous]
        [HttpPost("client-info")]
        public async Task<IActionResult> AddClientInfo([FromBody] ClientInfoDto clientInfoDto)
        {
            try
            {
                await _statsService.PathData(clientInfoDto);

                return Ok();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error updating the usage statistics");
                Console.WriteLine(e);
                throw;
            }
        }
    }
}