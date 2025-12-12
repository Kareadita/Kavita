using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.DTOs;
using API.DTOs.Filtering;
using API.DTOs.Metadata;
using API.DTOs.Uploads;
using API.Entities;
using API.Extensions;
using API.Helpers;
using API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TaskScheduler = API.Services.TaskScheduler;

namespace API.Controllers;

/// <summary>
/// All APIs here are subject to be removed and are no longer maintained
/// </summary>
[Route("api/")]
public class DeprecatedController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizationService _localizationService;
    private readonly ITaskScheduler _taskScheduler;
    private readonly ILogger<DeprecatedController> _logger;

    public DeprecatedController(IUnitOfWork unitOfWork, ILocalizationService localizationService, ITaskScheduler taskScheduler, ILogger<DeprecatedController> logger)
    {
        _unitOfWork = unitOfWork;
        _localizationService = localizationService;
        _taskScheduler = taskScheduler;
        _logger = logger;
    }

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
        var pagedList = await _unitOfWork.SeriesRepository.GetWantToReadForUserAsync(UserId, userParams, filterDto);
        Response.AddPaginationHeader(pagedList.CurrentPage, pagedList.PageSize, pagedList.TotalCount, pagedList.TotalPages);

        await _unitOfWork.SeriesRepository.AddSeriesModifiers(UserId, pagedList);

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
        return Ok(await _unitOfWork.ChapterRepository.GetChapterMetadataDtoAsync(chapterId));
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
            await _unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdAsync(libraryId, userId, userParams, filterDto);

        // Apply progress/rating information (I can't work out how to do this in initial query)
        if (series == null) return BadRequest(await _localizationService.Translate(UserId, "no-series"));

        await _unitOfWork.SeriesRepository.AddSeriesModifiers(userId, series);

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
    [ResponseCache(CacheProfileName = "Instant")]
    [HttpPost("series/recently-added")]
    [Obsolete("use recently-added-v2. Will be removed in v0.9.0")]
    public async Task<ActionResult<IEnumerable<SeriesDto>>> GetRecentlyAdded(FilterDto filterDto, [FromQuery] UserParams userParams, [FromQuery] int libraryId = 0)
    {
        var userId = UserId;
        var series =
            await _unitOfWork.SeriesRepository.GetRecentlyAdded(libraryId, userId, userParams, filterDto);

        // Apply progress/rating information (I can't work out how to do this in initial query)
        if (series == null) return BadRequest(await _localizationService.Translate(UserId, "no-series"));

        await _unitOfWork.SeriesRepository.AddSeriesModifiers(userId, series);

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
            await _unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdAsync(libraryId, userId, userParams, filterDto);

        // Apply progress/rating information (I can't work out how to do this in initial query)
        if (series == null) return BadRequest(await _localizationService.Translate(UserId, "no-series"));

        await _unitOfWork.SeriesRepository.AddSeriesModifiers(userId, series);

        Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);

        return Ok(series);
    }

    /// <summary>
    /// Replaces chapter cover image and locks it with a base64 encoded image. This will update the parent volume's cover image.
    /// </summary>
    /// <param name="uploadFileDto">Does not use Url property</param>
    /// <returns></returns>
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [HttpPost("upload/reset-chapter-lock")]
    [Obsolete("Use LockCover in UploadFileDto, will be removed in v0.9.0")]
    public async Task<ActionResult> ResetChapterLock(UploadFileDto uploadFileDto)
    {
        try
        {
            var chapter = await _unitOfWork.ChapterRepository.GetChapterAsync(uploadFileDto.Id);
            if (chapter == null) return BadRequest(await _localizationService.Translate(UserId, "chapter-doesnt-exist"));
            var originalFile = chapter.CoverImage;

            chapter.CoverImage = string.Empty;
            chapter.CoverImageLocked = false;
            _unitOfWork.ChapterRepository.Update(chapter);

            var volume = (await _unitOfWork.VolumeRepository.GetVolumeByIdAsync(chapter.VolumeId))!;
            volume.CoverImage = chapter.CoverImage;
            _unitOfWork.VolumeRepository.Update(volume);

            var series = (await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(volume.SeriesId))!;

            if (_unitOfWork.HasChanges())
            {
                await _unitOfWork.CommitAsync();
                if (originalFile != null) System.IO.File.Delete(originalFile);
                await _taskScheduler.RefreshSeriesMetadata(series.LibraryId, series.Id, true);
                return Ok();
            }

        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an issue resetting cover lock for Chapter {Id}", uploadFileDto.Id);
            await _unitOfWork.RollbackAsync();
        }

        return BadRequest(await _localizationService.Translate(UserId, "reset-chapter-lock"));
    }


}
