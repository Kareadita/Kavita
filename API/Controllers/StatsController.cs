using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.DTOs.Statistics;
using API.Entities;
using API.Extensions;
using API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

public class StatsController : BaseApiController
{
    private readonly IStatisticService _statService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<AppUser> _userManager;

    public StatsController(IStatisticService statService, IUnitOfWork unitOfWork, UserManager<AppUser> userManager)
    {
        _statService = statService;
        _unitOfWork = unitOfWork;
        _userManager = userManager;
    }

    [HttpGet("user/{userId}/read")]
    [ResponseCache(CacheProfileName = "Statistics")]
    public async Task<ActionResult<UserReadStatistics>> GetUserReadStatistics(int userId)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
        if (user.Id != userId && !await _userManager.IsInRoleAsync(user, PolicyConstants.AdminRole))
            return Unauthorized("You are not authorized to view another user's statistics");

        return Ok(await _statService.GetUserReadStatistics(userId, new List<int>()));
    }

    [Authorize("RequireAdminRole")]
    [HttpGet("server/year")]
    [ResponseCache(CacheProfileName = "Statistics")]
    public async Task<ActionResult<IEnumerable<YearSpread>>> GetYearStatistics()
    {
        return Ok(await _statService.GetYearSpread());
    }

}
