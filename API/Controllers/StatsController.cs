using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Statistics;
using API.DTOs.Stats.V3;
using API.DTOs.Stats.V3.ClientDevice;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Middleware;
using API.Services;
using API.Services.Tasks.Scanner.Parser;
using CsvHelper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MimeTypes;

namespace API.Controllers;

#nullable enable

public class StatsController(
    IStatisticService statService,
    IUnitOfWork unitOfWork,
    UserManager<AppUser> userManager,
    ILocalizationService localizationService,
    IDirectoryService directoryService)
    : BaseApiController
{
    [HttpGet("user/{userId}/read")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    public async Task<ActionResult<UserReadStatistics>> GetUserReadStatistics(int userId)
    {
        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(Username!);
        if (user!.Id != userId && !await userManager.IsInRoleAsync(user, PolicyConstants.AdminRole))
            return Unauthorized(await localizationService.Translate(UserId, "stats-permission-denied"));

        return Ok(await statService.GetUserReadStatistics(userId, new List<int>()));
    }

    [Authorize(PolicyGroups.AdminPolicy)]
    [HttpGet("server/stats")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    public async Task<ActionResult<ServerStatisticsDto>> GetHighLevelStats()
    {
        return Ok(await statService.GetServerStatistics());
    }

    [Authorize(PolicyGroups.AdminPolicy)]
    [HttpGet("server/count/year")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    public async Task<ActionResult<IEnumerable<StatCount<int>>>> GetYearStatistics()
    {
        return Ok(await statService.GetYearCount());
    }

    [Authorize(PolicyGroups.AdminPolicy)]
    [HttpGet("server/count/publication-status")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    public async Task<ActionResult<IEnumerable<StatCount<PublicationStatus>>>> GetPublicationStatus()
    {
        return Ok(await statService.GetPublicationCount());
    }

    [Authorize(PolicyGroups.AdminPolicy)]
    [HttpGet("server/count/manga-format")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    public async Task<ActionResult<IEnumerable<StatCount<MangaFormat>>>> GetMangaFormat()
    {
        return Ok(await statService.GetMangaFormatCount());
    }

    [Authorize(PolicyGroups.AdminPolicy)]
    [HttpGet("server/top/years")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    public async Task<ActionResult<IEnumerable<StatCount<int>>>> GetTopYears()
    {
        return Ok(await statService.GetTopYears());
    }

    /// <summary>
    /// Returns users with the top reads in the server
    /// </summary>
    /// <param name="days"></param>
    /// <returns></returns>
    [Authorize(PolicyGroups.AdminPolicy)]
    [HttpGet("server/top/users")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    public async Task<ActionResult<IEnumerable<TopReadDto>>> GetTopReads(int days = 0)
    {
        return Ok(await statService.GetTopUsers(days));
    }

    /// <summary>
    /// A breakdown of different files, their size, and format
    /// </summary>
    /// <returns></returns>
    [Authorize(PolicyGroups.AdminPolicy)]
    [HttpGet("server/file-breakdown")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    public async Task<ActionResult<IEnumerable<FileExtensionBreakdownDto>>> GetFileSize()
    {
        return Ok(await statService.GetFileBreakdown());
    }

    /// <summary>
    /// Generates a csv of all file paths for a given extension
    /// </summary>
    /// <returns></returns>
    [Authorize(PolicyGroups.AdminPolicy)]
    [HttpGet("server/file-extension")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    public async Task<ActionResult> DownloadFilesByExtension(string fileExtension)
    {
        if (!Regex.IsMatch(fileExtension, Parser.SupportedExtensions))
        {
            return BadRequest("Invalid file format");
        }
        var tempFile = Path.Join(directoryService.TempDirectory,
            $"file_breakdown_{fileExtension.Replace(".", string.Empty)}.csv");

        if (!directoryService.FileSystem.File.Exists(tempFile))
        {
            var results = await statService.GetFilesByExtension(fileExtension);
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
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    public async Task<ActionResult<IEnumerable<PagesReadOnADayCount<DateTime>>>> ReadCountByDay(int userId = 0, int days = 0)
    {
        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(Username!);
        var isAdmin = User.IsInRole(PolicyConstants.AdminRole);
        if (!isAdmin && userId != user!.Id) return BadRequest();

        return Ok(await statService.ReadCountByDay(userId, days));
    }

    [HttpGet("day-breakdown")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    public async Task<ActionResult<IEnumerable<StatCount<DayOfWeek>>>> GetDayBreakdown(int userId = 0)
    {
        if (userId == 0)
        {
            var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(Username!);
            var isAdmin = await unitOfWork.UserRepository.IsUserAdminAsync(user);
            if (!isAdmin) return BadRequest();
        }

        return Ok(statService.GetDayBreakdown(userId));
    }



    [HttpGet("user/reading-history")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    public async Task<ActionResult<IEnumerable<ReadHistoryEvent>>> GetReadingHistory(int userId)
    {
        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(Username!);
        var isAdmin = User.IsInRole(PolicyConstants.AdminRole);
        if (!isAdmin && userId != user!.Id) return BadRequest();

        return Ok(await statService.GetReadingHistory(userId));
    }

    /// <summary>
    /// Returns a count of pages read per year for a given userId.
    /// </summary>
    /// <param name="userId">If userId is 0 and user is not an admin, API will default to userId</param>
    /// <returns></returns>
    [HttpGet("pages-per-year")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    public async Task<ActionResult<IEnumerable<StatCount<int>>>> GetPagesReadPerYear(int userId = 0)
    {
        var isAdmin = User.IsInRole(PolicyConstants.AdminRole);
        if (!isAdmin) userId = await unitOfWork.UserRepository.GetUserIdByUsernameAsync(Username!);
        return Ok(statService.GetPagesReadCountByYear(userId));
    }

    /// <summary>
    /// Returns a count of words read per year for a given userId.
    /// </summary>
    /// <param name="userId">If userId is 0 and user is not an admin, API will default to userId</param>
    /// <returns></returns>
    [HttpGet("words-per-year")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    public async Task<ActionResult<IEnumerable<StatCount<int>>>> GetWordsReadPerYear(int userId = 0)
    {
        var isAdmin = User.IsInRole(PolicyConstants.AdminRole);
        if (!isAdmin) userId = await unitOfWork.UserRepository.GetUserIdByUsernameAsync(Username!);
        return Ok(statService.GetWordsReadCountByYear(userId));
    }

    #region Device Insights

    /// <summary>
    /// Returns client type breakdown for the current month
    /// </summary>
    /// <returns></returns>
    [HttpGet("device/client-type")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    [Authorize(PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<DeviceClientBreakdownDto>> GetClientTypeBreakdown()
    {
        return Ok(await statService.GetClientTypeBreakdown(DateTime.UtcNow.StartOfMonth()));
    }


    /// <summary>
    /// Desktop vs Mobile spread over last month
    /// </summary>
    /// <returns></returns>
    [HttpGet("device/device-type")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    [Authorize(PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<StatCount<string>>> GetDeviceTypeCounts()
    {
        // Mobile vs Desktop Ratio - Overall usage pattern
        return Ok(await statService.GetDeviceTypeCounts(DateTime.UtcNow.StartOfMonth()));
    }

    #endregion


    #region Reading History

    [HttpGet("reading-activity")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    public async Task<ActionResult<ReadingActivityGraphDto>> GetReadingActivity([FromQuery] StatsFilterDto filter, int userId, int year)
    {
        await CleanStatsFilter(filter, UserId);

        return Ok(await statService.GetReadingActivityGraphData(filter, userId, year, UserId));
    }

    #endregion

    #region Profile Stats


    [ProfilePrivacy]
    [HttpGet("reading-pace")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    public async Task<ActionResult<ReadingPaceDto>> GetReadingPace([FromQuery] StatsFilterDto filter, int userId, int year)
    {
        await CleanStatsFilter(filter, UserId);

        return Ok(await statService.GetReadingPaceForUser(filter, userId, year));
    }

    /// <summary>
    /// Returns each format type read
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    [ProfilePrivacy]
    [HttpGet("preferred-format")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    public async Task<ActionResult<IList<StatCount<MangaFormat>>>> GetPreferredMangaFormat([FromQuery] StatsFilterDto filter, int userId)
    {
        await CleanStatsFilter(filter, UserId);

        return Ok(await statService.GetPreferredFormatForUser(filter, userId, UserId));
    }

    /// <summary>
    /// Returns top 10 genres that user likes reading
    /// </summary>
    /// <param name="filter"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    [ProfilePrivacy]
    [HttpGet("genre-breakdown")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    public async Task<ActionResult<BreakDownDto<string>>> GetGenreBreakdown([FromQuery] StatsFilterDto filter, int userId)
    {
        await CleanStatsFilter(filter, UserId);

        return Ok(await statService.GetGenreBreakdownForUser(filter, userId, UserId));
    }

    /// <summary>
    /// Returns top 10 tags that user likes reading
    /// </summary>
    /// <param name="filter"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    [ProfilePrivacy]
    [HttpGet("tag-breakdown")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    public async Task<ActionResult<BreakDownDto<string>>> GetTagBreakdown([FromQuery] StatsFilterDto filter, int userId)
    {
        await CleanStatsFilter(filter, UserId);

        return Ok(await statService.GetTagBreakdownForUser(filter, userId, UserId));
    }


    [ProfilePrivacy]
    [HttpGet("page-spread")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    public async Task<ActionResult<SpreadStatsDto>> GetPageSpread([FromQuery] StatsFilterDto filter, int userId)
    {
        await CleanStatsFilter(filter, UserId);

        return Ok(await statService.GetPageSpreadForUser(filter, userId, UserId));
    }

    [ProfilePrivacy]
    [HttpGet("word-spread")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    public async Task<ActionResult<SpreadStatsDto>> GetWordSpread([FromQuery] StatsFilterDto filter, int userId)
    {
        await CleanStatsFilter(filter, UserId);

        return Ok(await statService.GetWordSpreadForUser(filter, userId, UserId));
    }

    [ProfilePrivacy]
    [HttpGet("favorite-authors")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    public async Task<ActionResult<MostReadAuthorsDto>> GetMostReadAuthors([FromQuery] StatsFilterDto filter, int userId)
    {
        await CleanStatsFilter(filter, UserId);

        return Ok(await statService.GetMostReadAuthors(filter, userId, UserId));
    }

    /// <summary>
    /// Returns the avg time read by hour in the given filter
    /// </summary>
    /// <param name="filter"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    [ProfilePrivacy]
    [HttpGet("avg-time-by-hour")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    public async Task<ActionResult<ReadTimeByHourDto>> GetAverageTimePerHour([FromQuery] StatsFilterDto filter, int userId)
    {
        await CleanStatsFilter(filter, UserId);

        var dto = await statService.GetTimeReadingByHour(filter, userId, UserId);
        if (dto == null) return BadRequest();

        return Ok(dto);
    }

    /// <summary>
    /// Gives the total amount of chapters reads per month, filters start & end date will not apply
    /// </summary>
    /// <param name="filter"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    [ProfilePrivacy]
    [HttpGet("reads-by-month")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    public async Task<ActionResult<IList<StatCount<YearMonthGroupingDto>>>> GetReadsPerMonth([FromQuery] StatsFilterDto filter, int userId)
    {
        await CleanStatsFilter(filter, UserId);

        return Ok(await statService.GetReadsPerMonth(filter, userId, UserId));
    }

    /// <summary>
    /// Returns the total amount reads in the given filter
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    [ProfilePrivacy]
    [HttpGet("total-reads")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    public async Task<ActionResult<int>> GetTotalReads(int userId)
    {
        return Ok(await statService.GetTotalReads(userId, UserId));
    }

    [ProfilePrivacy]
    [HttpGet("user-stats")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    public async Task<ActionResult<ProfileStatBarDto>> GetStatsForUserBar([FromQuery] StatsFilterDto filter, int userId)
    {
        await CleanStatsFilter(filter, userId);
        return Ok(await statService.GetUserStatBar(filter, userId, UserId));
    }

    // TODO: Can we cache this? Can we make an attribute to cache methods based on keys?
    /// <summary>
    /// Cleans the stats filter to only include valid data. I.e. only requests libraries the user has access to
    /// </summary>
    /// <param name="filter"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    private async Task CleanStatsFilter(StatsFilterDto filter, int userId)
    {
        var libraries = await unitOfWork.LibraryRepository.GetLibraryIdsForUserIdAsync(userId);

        filter.Libraries = filter.Libraries.Intersect(libraries).ToList();
    }

    #endregion

}
