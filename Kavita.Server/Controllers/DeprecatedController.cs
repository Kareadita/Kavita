using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Common.Helpers;
using Kavita.Models.Constants;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Account;
using Kavita.Models.DTOs.Filtering;
using Kavita.Models.DTOs.Metadata;
using Kavita.Models.DTOs.Progress;
using Kavita.Models.DTOs.Statistics;
using Kavita.Models.DTOs.Uploads;
using Kavita.Server.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.Controllers;

/// <summary>
/// All APIs here are subject to be removed and are no longer maintained. Will be removed v0.9.0
/// </summary>
[Route("api/")]
public class DeprecatedController(
    IUnitOfWork unitOfWork,
    ILocalizationService localizationService,
    ITaskScheduler taskScheduler,
    ILogger<DeprecatedController> logger,
    IStatisticService statService,
    IMapper mapper)
    : BaseApiController
{
    /// <summary>
    /// Return all Series that are in the current logged-in user's Want to Read list, filtered (deprecated, use v2)
    /// </summary>
    /// <remarks>This will be removed in v0.9.0</remarks>
    /// <param name="userParams"></param>
    /// <param name="filterDto"></param>
    /// <returns></returns>
    [HttpPost("want-to-read")]
    [Obsolete("use v2 instead. This will be removed in v0.9.0")]
    public async Task<ActionResult<PagedList<SeriesDto>>> GetWantToRead([FromQuery] UserParams? userParams, FilterDto filterDto)
    {
        userParams ??= new UserParams();
        var pagedList = await unitOfWork.SeriesRepository.GetWantToReadForUserAsync(UserId, userParams, filterDto);
        Response.AddPaginationHeader(pagedList.CurrentPage, pagedList.PageSize, pagedList.TotalCount, pagedList.TotalPages);

        return Ok(pagedList);
    }

    /// <summary>
    /// All chapter entities will load this data by default. Will not be maintained as of v0.8.1
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [Obsolete("All chapter entities will load this data by default. Will be removed in v0.9.0")]
    [HttpGet("series/chapter-metadata")]
    public async Task<ActionResult<ChapterMetadataDto>> GetChapterMetadata(int chapterId)
    {
        return Ok(await unitOfWork.ChapterRepository.GetChapterMetadataDtoAsync(chapterId));
    }

    /// <summary>
    /// Gets series with the applied Filter
    /// </summary>
    /// <remarks>This is considered v1 and no longer used by Kavita, but will be supported for sometime. See series/v2</remarks>
    /// <param name="libraryId"></param>
    /// <param name="userParams"></param>
    /// <param name="filterDto"></param>
    /// <returns></returns>
    [HttpPost("series")]
    [Obsolete("use v2. Will be removed in v0.9.0")]
    public async Task<ActionResult<IEnumerable<SeriesDto>>> GetSeriesForLibrary(int libraryId, [FromQuery] UserParams userParams, [FromBody] FilterDto filterDto)
    {
        var userId = UserId;
        var series =
            await unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdAsync(libraryId, userId, userParams, filterDto);

        Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);

        return Ok(series);
    }

    /// <summary>
    /// Gets all recently added series. Obsolete, use recently-added-v2
    /// </summary>
    /// <param name="filterDto"></param>
    /// <param name="userParams"></param>
    /// <param name="libraryId"></param>
    /// <returns></returns>
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Minute)]
    [HttpPost("series/recently-added")]
    [Obsolete("use recently-added-v2. Will be removed in v0.9.0")]
    public async Task<ActionResult<IEnumerable<SeriesDto>>> GetRecentlyAdded(FilterDto filterDto, [FromQuery] UserParams userParams, [FromQuery] int libraryId = 0)
    {
        var userId = UserId;
        var series =
            await unitOfWork.SeriesRepository.GetRecentlyAdded(libraryId, userId, userParams, filterDto);

        Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);

        return Ok(series);
    }

    /// <summary>
    /// Returns all series for the library. Obsolete, use all-v2
    /// </summary>
    /// <param name="filterDto"></param>
    /// <param name="userParams"></param>
    /// <param name="libraryId"></param>
    /// <returns></returns>
    [HttpPost("series/all")]
    [Obsolete("Use all-v2. Will be removed in v0.9.0")]
    public async Task<ActionResult<IEnumerable<SeriesDto>>> GetAllSeries(FilterDto filterDto, [FromQuery] UserParams userParams, [FromQuery] int libraryId = 0)
    {
        var userId = UserId;
        var series =
            await unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdAsync(libraryId, userId, userParams, filterDto);

        Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);

        return Ok(series);
    }

    /// <summary>
    /// Replaces chapter cover image and locks it with a base64 encoded image. This will update the parent volume's cover image.
    /// </summary>
    /// <param name="uploadCoverFileDto">Does not use Url property</param>
    /// <returns></returns>
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [HttpPost("upload/reset-chapter-lock")]
    [Obsolete("Use LockCover in UploadFileDto, will be removed in v0.9.0")]
    public async Task<ActionResult> ResetChapterLock(UploadCoverFileDto uploadCoverFileDto)
    {
        try
        {
            var chapter = await unitOfWork.ChapterRepository.GetChapterAsync(uploadCoverFileDto.Id);
            if (chapter == null) return BadRequest(await localizationService.Translate(UserId, "chapter-doesnt-exist"));
            var originalFile = chapter.CoverImage;

            chapter.CoverImage = string.Empty;
            chapter.CoverImageLocked = false;
            unitOfWork.ChapterRepository.Update(chapter);

            var volume = (await unitOfWork.VolumeRepository.GetVolumeByIdAsync(chapter.VolumeId))!;
            volume.CoverImage = chapter.CoverImage;
            unitOfWork.VolumeRepository.Update(volume);

            var series = (await unitOfWork.SeriesRepository.GetSeriesByIdAsync(volume.SeriesId))!;

            if (unitOfWork.HasChanges())
            {
                await unitOfWork.CommitAsync();
                if (originalFile != null) System.IO.File.Delete(originalFile);
                await taskScheduler.RefreshSeriesMetadata(series.LibraryId, series.Id, true);
                return Ok();
            }

        }
        catch (Exception e)
        {
            logger.LogError(e, "There was an issue resetting cover lock for Chapter {Id}", uploadCoverFileDto.Id);
            await unitOfWork.RollbackAsync();
        }

        return BadRequest(await localizationService.Translate(UserId, "reset-chapter-lock"));
    }


    [HttpGet("stats/user/reading-history")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    [Obsolete("Will be removed in v0.9.0")]
    public async Task<ActionResult<IEnumerable<ReadHistoryEvent>>> GetReadingHistory(int userId)
    {
        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(Username!);
        var isAdmin = User.IsInRole(PolicyConstants.AdminRole);
        if (!isAdmin && userId != user!.Id) return BadRequest();

        return Ok(await statService.GetReadingHistory(userId));
    }

    [Authorize(PolicyGroups.AdminPolicy)]
    [HttpGet("stats/server/top/years")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    [Obsolete("Will be removed in v0.9.0")]
    public async Task<ActionResult<IEnumerable<StatCount<int>>>> GetTopYears()
    {
        return Ok(await statService.GetTopYears());
    }

    /// <summary>
    /// Returns reading history events for a give or all users, broken up by day, and format
    /// </summary>
    /// <param name="userId">If 0, defaults to all users, else just userId</param>
    /// <param name="days">If 0, defaults to all time, else just those days asked for</param>
    /// <returns></returns>
    [HttpGet("stats/reading-count-by-day")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    [Obsolete("Will be removed in v0.9.0")]
    public async Task<ActionResult<IEnumerable<StatCountWithFormat<DateTime>>>> ReadCountByDay(int userId = 0, int days = 0)
    {
        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(Username!);
        var isAdmin = User.IsInRole(PolicyConstants.AdminRole);
        if (!isAdmin && userId != user!.Id) return BadRequest();

        return Ok(await statService.ReadCountByDay(userId, days));
    }

    [Authorize(PolicyGroups.AdminPolicy)]
    [HttpGet("server/count/year")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    [Obsolete("Will be removed in v0.9.0")]
    public async Task<ActionResult<IEnumerable<StatCount<int>>>> GetYearStatistics()
    {
        return Ok(await statService.GetYearCount());
    }

    /// <summary>
    /// Returns users with the top reads in the server
    /// </summary>
    /// <param name="days"></param>
    /// <returns></returns>
    [Authorize(PolicyGroups.AdminPolicy)]
    [HttpGet("stats/server/top/users")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Statistics)]
    [Obsolete("Will be removed in v0.9.0")]
    public async Task<ActionResult<IEnumerable<TopReadDto>>> GetTopReads(int days = 0)
    {
        return Ok(await statService.GetTopUsers(days));
    }

    /// <summary>
    /// Get all progress events for a given chapter
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpGet("reader/all-chapter-progress")]
    [Obsolete("Will be removed in v0.9.0")]
    public async Task<ActionResult<IEnumerable<FullProgressDto>>> GetProgressForChapter(int chapterId)
    {
        var userId = User.IsInRole(PolicyConstants.AdminRole) ? 0 : UserId;
        return Ok(await unitOfWork.AppUserProgressRepository.GetUserProgressForChapter(chapterId, userId));
    }

    /// <summary>
    /// Quick Reads are series that should be readable in less than 10 in total and are not Ongoing in release.
    /// </summary>
    /// <param name="libraryId">Library to restrict series to</param>
    /// <param name="userParams">Pagination</param>
    /// <returns></returns>
    [HttpGet("recommended/quick-reads")]
    [Obsolete("Will be removed in v0.9.0")]
    public async Task<ActionResult<PagedList<SeriesDto>>> GetQuickReads(int libraryId, [FromQuery] UserParams? userParams)
    {
        userParams ??= UserParams.Default;
        var series = await unitOfWork.SeriesRepository.GetQuickReads(UserId, libraryId, userParams);

        Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);
        return Ok(series);
    }

    /// <summary>
    /// Quick Catchup Reads are series that should be readable in less than 10 in total and are Ongoing in release.
    /// </summary>
    /// <param name="libraryId">Library to restrict series to</param>
    /// <param name="userParams"></param>
    /// <returns></returns>
    [HttpGet("recommended/quick-catchup-reads")]
    [Obsolete("Will be removed in v0.9.0")]
    public async Task<ActionResult<PagedList<SeriesDto>>> GetQuickCatchupReads(int libraryId, [FromQuery] UserParams? userParams)
    {
        userParams ??= UserParams.Default;
        var series = await unitOfWork.SeriesRepository.GetQuickCatchupReads(UserId, libraryId, userParams);

        Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);
        return Ok(series);
    }

    /// <summary>
    /// Highly Rated based on other users ratings. Will pull series with ratings > 4.0, weighted by count of other users.
    /// </summary>
    /// <param name="libraryId">Library to restrict series to</param>
    /// <param name="userParams">Pagination</param>
    /// <returns></returns>
    [HttpGet("recommended/highly-rated")]
    [Obsolete("Will be removed in v0.9.0")]
    public async Task<ActionResult<PagedList<SeriesDto>>> GetHighlyRated(int libraryId, [FromQuery] UserParams? userParams)
    {
        var userId = UserId;
        userParams ??= UserParams.Default;

        var series = await unitOfWork.SeriesRepository.GetHighlyRated(userId, libraryId, userParams);

        Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);

        return Ok(series);
    }

    /// <summary>
    /// Chooses a random genre and shows series that are in that without reading progress
    /// </summary>
    /// <param name="libraryId">Library to restrict series to</param>
    /// <param name="genreId">Genre Id</param>
    /// <param name="userParams">Pagination</param>
    /// <returns></returns>
    [HttpGet("recommended/more-in")]
    [Obsolete("Will be removed in v0.9.0")]
    public async Task<ActionResult<PagedList<SeriesDto>>> GetMoreIn(int libraryId, int genreId, [FromQuery] UserParams? userParams)
    {
        var userId = UserId;

        userParams ??= UserParams.Default;
        var series = await unitOfWork.SeriesRepository.GetMoreIn(userId, libraryId, genreId, userParams);

        Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);
        return Ok(series);
    }

    /// <summary>
    /// Series that are fully read by the user in no particular order
    /// </summary>
    /// <param name="libraryId">Library to restrict series to</param>
    /// <param name="userParams">Pagination</param>
    /// <returns></returns>
    [HttpGet("recommended/rediscover")]
    [Obsolete("Will be removed in v0.9.0")]
    public async Task<ActionResult<PagedList<SeriesDto>>> GetRediscover(int libraryId, [FromQuery] UserParams? userParams)
    {
        userParams ??= UserParams.Default;
        var series = await unitOfWork.SeriesRepository.GetRediscover(UserId, libraryId, userParams);

        Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);
        return Ok(series);
    }

    [Obsolete("Will be removed in v0.9.0")]
    [HttpGet("users/myself")]
    public async Task<ActionResult<IEnumerable<MemberDto>>> GetMyself()
    {
        var users = await unitOfWork.UserRepository.GetAllUsersAsync();
        return Ok(users.Where(u => u.UserName == Username!).DefaultIfEmpty().Select(u => mapper.Map<MemberDto>(u)).SingleOrDefault());
    }

}
