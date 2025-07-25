using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.DTOs.Statistics;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using API.Services;
using API.Services.Plus;
using API.Services.Tasks.Scanner.Parser;
using CsvHelper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MimeTypes;

namespace API.Controllers;

#nullable enable

public class StatsController : BaseApiController
{
    private readonly IStatisticService _statService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<AppUser> _userManager;
    private readonly ILocalizationService _localizationService;
    private readonly ILicenseService _licenseService;
    private readonly IDirectoryService _directoryService;

    public StatsController(IStatisticService statService, IUnitOfWork unitOfWork,
        UserManager<AppUser> userManager, ILocalizationService localizationService,
        ILicenseService licenseService, IDirectoryService directoryService)
    {
        _statService = statService;
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _localizationService = localizationService;
        _licenseService = licenseService;
        _directoryService = directoryService;
    }

    [HttpGet("user/{userId}/read")]
    [ResponseCache(CacheProfileName = "Statistics")]
    public async Task<ActionResult<UserReadStatistics>> GetUserReadStatistics(int userId)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
        if (user!.Id != userId && !await _userManager.IsInRoleAsync(user, PolicyConstants.AdminRole))
            return Unauthorized(await _localizationService.Translate(User.GetUserId(), "stats-permission-denied"));

        return Ok(await _statService.GetUserReadStatistics(userId, new List<int>()));
    }

    [Authorize("RequireAdminRole")]
    [HttpGet("server/stats")]
    [ResponseCache(CacheProfileName = "Statistics")]
    public async Task<ActionResult<ServerStatisticsDto>> GetHighLevelStats()
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

    /// <summary>
    /// A breakdown of different files, their size, and format
    /// </summary>
    /// <returns></returns>
    [Authorize("RequireAdminRole")]
    [HttpGet("server/file-breakdown")]
    [ResponseCache(CacheProfileName = "Statistics")]
    public async Task<ActionResult<IEnumerable<FileExtensionBreakdownDto>>> GetFileSize()
    {
        return Ok(await _statService.GetFileBreakdown());
    }

    /// <summary>
    /// Generates a csv of all file paths for a given extension
    /// </summary>
    /// <returns></returns>
    [Authorize("RequireAdminRole")]
    [HttpGet("server/file-extension")]
    [ResponseCache(CacheProfileName = "Statistics")]
    public async Task<ActionResult> DownloadFilesByExtension(string fileExtension)
    {
        if (!Regex.IsMatch(fileExtension, Parser.SupportedExtensions))
        {
            return BadRequest("Invalid file format");
        }
        var tempFile = Path.Join(_directoryService.TempDirectory,
            $"file_breakdown_{fileExtension.Replace(".", string.Empty)}.csv");

        if (!_directoryService.FileSystem.File.Exists(tempFile))
        {
            var results = await _statService.GetFilesByExtension(fileExtension);
            await using var writer = new StreamWriter(tempFile);
            await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            await csv.WriteRecordsAsync(results);
        }

        return PhysicalFile(tempFile, MimeTypeMap.GetMimeType(Path.GetExtension(tempFile)),
            System.Web.HttpUtility.UrlEncode(Path.GetFileName(tempFile)), true);
    }


    /// <summary>
    /// Returns reading history events for a give or all users, broken up by day, and format
    /// </summary>
    /// <param name="userId">If 0, defaults to all users, else just userId</param>
    /// <param name="days">If 0, defaults to all time, else just those days asked for</param>
    /// <returns></returns>
    [HttpGet("reading-count-by-day")]
    [ResponseCache(CacheProfileName = "Statistics")]
    public async Task<ActionResult<IEnumerable<PagesReadOnADayCount<DateTime>>>> ReadCountByDay(int userId = 0, int days = 0)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
        var isAdmin = User.IsInRole(PolicyConstants.AdminRole);
        if (!isAdmin && userId != user!.Id) return BadRequest();

        return Ok(await _statService.ReadCountByDay(userId, days));
    }

    [HttpGet("day-breakdown")]
    [ResponseCache(CacheProfileName = "Statistics")]
    public async Task<ActionResult<IEnumerable<StatCount<DayOfWeek>>>> GetDayBreakdown(int userId = 0)
    {
        if (userId == 0)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            var isAdmin = await _unitOfWork.UserRepository.IsUserAdminAsync(user);
            if (!isAdmin) return BadRequest();
        }

        return Ok(_statService.GetDayBreakdown(userId));
    }



    [HttpGet("user/reading-history")]
    [ResponseCache(CacheProfileName = "Statistics")]
    public async Task<ActionResult<IEnumerable<ReadHistoryEvent>>> GetReadingHistory(int userId)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
        var isAdmin = User.IsInRole(PolicyConstants.AdminRole);
        if (!isAdmin && userId != user!.Id) return BadRequest();

        return Ok(await _statService.GetReadingHistory(userId));
    }

    /// <summary>
    /// Returns a count of pages read per year for a given userId.
    /// </summary>
    /// <param name="userId">If userId is 0 and user is not an admin, API will default to userId</param>
    /// <returns></returns>
    [HttpGet("pages-per-year")]
    [ResponseCache(CacheProfileName = "Statistics")]
    public async Task<ActionResult<IEnumerable<StatCount<int>>>> GetPagesReadPerYear(int userId = 0)
    {
        var isAdmin = User.IsInRole(PolicyConstants.AdminRole);
        if (!isAdmin) userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
        return Ok(_statService.GetPagesReadCountByYear(userId));
    }

    /// <summary>
    /// Returns a count of words read per year for a given userId.
    /// </summary>
    /// <param name="userId">If userId is 0 and user is not an admin, API will default to userId</param>
    /// <returns></returns>
    [HttpGet("words-per-year")]
    [ResponseCache(CacheProfileName = "Statistics")]
    public async Task<ActionResult<IEnumerable<StatCount<int>>>> GetWordsReadPerYear(int userId = 0)
    {
        var isAdmin = User.IsInRole(PolicyConstants.AdminRole);
        if (!isAdmin) userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
        return Ok(_statService.GetWordsReadCountByYear(userId));
    }

}
