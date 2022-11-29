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
    [HttpGet("server/stats")]
    [ResponseCache(CacheProfileName = "Statistics")]
    public async Task<ActionResult<IEnumerable<ServerStatistics>>> GetHighLevelStats()
    {
        return Ok(await _statService.GetServerStatistics());
    }

    [Authorize("RequireAdminRole")]
    [HttpGet("server/count/year")]
    [ResponseCache(CacheProfileName = "Statistics")]
    public async Task<ActionResult<IEnumerable<YearCountDto>>> GetYearStatistics()
    {
        return Ok(await _statService.GetYearCount());
    }

    [Authorize("RequireAdminRole")]
    [HttpGet("server/count/publication-status")]
    [ResponseCache(CacheProfileName = "Statistics")]
    public async Task<ActionResult<IEnumerable<PublicationCountDto>>> GetPublicationStatus()
    {
        return Ok(await _statService.GetPublicationCount());
    }

    [Authorize("RequireAdminRole")]
    [HttpGet("server/count/manga-format")]
    [ResponseCache(CacheProfileName = "Statistics")]
    public async Task<ActionResult<IEnumerable<MangaFormatCountDto>>> GetMangaFormat()
    {
        return Ok(await _statService.GetMangaFormatCount());
    }

    [Authorize("RequireAdminRole")]
    [HttpGet("server/top/years")]
    [ResponseCache(CacheProfileName = "Statistics")]
    public async Task<ActionResult<IEnumerable<YearCountDto>>> GetTopYears()
    {
        return Ok(await _statService.GetTopYears());
    }

    /// <summary>
    /// Returns
    /// </summary>
    /// <param name="days"></param>
    /// <returns></returns>
    [Authorize("RequireAdminRole")]
    [HttpGet("server/top/users")]
    [ResponseCache(CacheProfileName = "Statistics")]
    public async Task<ActionResult<IEnumerable<TopReadDto>>> GetTopReads(int days = 0)
    {
        return Ok(await _statService.GetTopUsers(days));
    }

    [Authorize("RequireAdminRole")]
    [HttpGet("server/file-breakdown")]
    [ResponseCache(CacheProfileName = "Statistics")]
    public async Task<ActionResult<IEnumerable<FileExtensionBreakdownDto>>> GetFileSize()
    {
        return Ok(await _statService.GetFileBreakdown());
    }

}
