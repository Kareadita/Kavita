using System;
using System.Threading.Tasks;
using API.DTOs.Stats;
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
                await _statsService.RecordClientInfo(clientInfoDto);

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating the usage statistics");
                throw;
            }
        }
    }
}
