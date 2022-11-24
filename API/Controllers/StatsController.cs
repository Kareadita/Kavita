using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs.Statistics;
using API.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

public class StatsController : BaseApiController
{
    private readonly IStatisticService _statService;

    public StatsController(IStatisticService statService)
    {
        _statService = statService;
    }

    [HttpGet("user/{userId}/read")]
    [ResponseCache(CacheProfileName = "Statistics")]
    public async Task<ActionResult<UserReadStatistics>> GetUserReadStatistics(int userId)
    {
        return Ok(await _statService.GetUserReadStatistics(userId, new List<int>()));
    }

}
