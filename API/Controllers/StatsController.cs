using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.DTOs.Statistics;
using API.Entities;
using API.Entities.Enums;
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
    public async Task<ActionResult<ServerStatistics>> GetHighLevelStats()
    {
        return Ok(await _statService.GetServerStatistics());
    }

    [Authorize("RequireAdminRole")]
    [HttpGet("server/count/year")]
    [ResponseCache(CacheProfileName = "Statistics")]
    public async Task<ActionResult<IEnumerable<StatCount<int>>>> GetYearStatistics()
    {
        return Ok(await _statService.GetYearCount());
    }

    [Authorize("RequireAdminRole")]
    [HttpGet("server/count/publication-status")]
    [ResponseCache(CacheProfileName = "Statistics")]
    public async Task<ActionResult<IEnumerable<StatCount<PublicationStatus>>>> GetPublicationStatus()
    {
        return Ok(await _statService.GetPublicationCount());
    }

    [Authorize("RequireAdminRole")]
    [HttpGet("server/count/manga-format")]
    [ResponseCache(CacheProfileName = "Statistics")]
    public async Task<ActionResult<IEnumerable<StatCount<MangaFormat>>>> GetMangaFormat()
    {
        return Ok(await _statService.GetMangaFormatCount());
    }

    [Authorize("RequireAdminRole")]
    [HttpGet("server/top/years")]
    [ResponseCache(CacheProfileName = "Statistics")]
    public async Task<ActionResult<IEnumerable<StatCount<int>>>> GetTopYears()
    {
        return Ok(await _statService.GetTopYears());
    }

    /// <summary>
    /// Returns users with the top reads in the server
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


    /// <summary>
    /// Returns reading history events for a give or all users, broken up by day, and format
    /// </summary>
    /// <param name="userId">If 0, defaults to all users, else just userId</param>
    /// <returns></returns>
    [HttpGet("reading-count-by-day")]
    [ResponseCache(CacheProfileName = "Statistics")]
    public async Task<ActionResult<IEnumerable<PagesReadOnADayCount<DateTime>>>> ReadCountByDay(int userId = 0)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
        var isAdmin = User.IsInRole(PolicyConstants.AdminRole);
        if (!isAdmin && userId != user.Id) return Unauthorized();

        return Ok(await _statService.ReadCountByDay(userId));
    }


    [HttpGet("user/reading-history")]
    [ResponseCache(CacheProfileName = "Statistics")]
    public async Task<ActionResult<IEnumerable<ReadHistoryEvent>>> GetReadingHistory(int userId)
    {
        // TODO: Put a check in if the calling user is said userId or has admin

        return Ok(await _statService.GetReadingHistory(userId));
    }

}
