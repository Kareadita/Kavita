using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.Filtering;
using API.DTOs.SeriesDetail;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Extensions;
using API.Helpers;
using API.Services;
using Kavita.Common;
using Kavita.Common.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers;

public class SeriesController : BaseApiController
{
    private readonly ILogger<SeriesController> _logger;
    private readonly ITaskScheduler _taskScheduler;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISeriesService _seriesService;


    public SeriesController(ILogger<SeriesController> logger, ITaskScheduler taskScheduler, IUnitOfWork unitOfWork, ISeriesService seriesService)
    {
        _logger = logger;
        _taskScheduler = taskScheduler;
        _unitOfWork = unitOfWork;
        _seriesService = seriesService;
    }

    [HttpPost]
    public async Task<ActionResult<IEnumerable<Series>>> GetSeriesForLibrary(int libraryId, [FromQuery] UserParams userParams, [FromBody] FilterDto filterDto)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
        var series =
            await _unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdAsync(libraryId, userId, userParams, filterDto);

        // Apply progress/rating information (I can't work out how to do this in initial query)
        if (series == null) return BadRequest("Could not get series for library");

        await _unitOfWork.SeriesRepository.AddSeriesModifiers(userId, series);

        Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);

        return Ok(series);
    }

    /// <summary>
    /// Fetches a Series for a given Id
    /// </summary>
    /// <param name="seriesId">Series Id to fetch details for</param>
    /// <returns></returns>
    /// <exception cref="KavitaException">Throws an exception if the series Id does exist</exception>
    [HttpGet("{seriesId:int}")]
    public async Task<ActionResult<SeriesDto>> GetSeries(int seriesId)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
        try
        {
            return Ok(await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, userId));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an issue fetching {SeriesId}", seriesId);
            throw new KavitaException("This series does not exist");
        }

    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpDelete("{seriesId}")]
    public async Task<ActionResult<bool>> DeleteSeries(int seriesId)
    {
        var username = User.GetUsername();
        _logger.LogInformation("Series {SeriesId} is being deleted by {UserName}", seriesId, username);

        return Ok(await _seriesService.DeleteMultipleSeries(new[] {seriesId}));
    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("delete-multiple")]
    public async Task<ActionResult> DeleteMultipleSeries(DeleteSeriesDto dto)
    {
        var username = User.GetUsername();
        _logger.LogInformation("Series {SeriesId} is being deleted by {UserName}", dto.SeriesIds, username);

        if (await _seriesService.DeleteMultipleSeries(dto.SeriesIds)) return Ok();

        return BadRequest("There was an issue deleting the series requested");
    }

    /// <summary>
    /// Returns All volumes for a series with progress information and Chapters
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpGet("volumes")]
    public async Task<ActionResult<IEnumerable<VolumeDto>>> GetVolumes(int seriesId)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
        return Ok(await _unitOfWork.VolumeRepository.GetVolumesDtoAsync(seriesId, userId));
    }

    [HttpGet("volume")]
    public async Task<ActionResult<VolumeDto>> GetVolume(int volumeId)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
        return Ok(await _unitOfWork.VolumeRepository.GetVolumeDtoAsync(volumeId, userId));
    }

    [HttpGet("chapter")]
    public async Task<ActionResult<ChapterDto>> GetChapter(int chapterId)
    {
        return Ok(await _unitOfWork.ChapterRepository.GetChapterDtoAsync(chapterId));
    }

    [HttpGet("chapter-metadata")]
    public async Task<ActionResult<ChapterDto>> GetChapterMetadata(int chapterId)
    {
        return Ok(await _unitOfWork.ChapterRepository.GetChapterMetadataDtoAsync(chapterId));
    }


    [HttpPost("update-rating")]
    public async Task<ActionResult> UpdateSeriesRating(UpdateSeriesRatingDto updateSeriesRatingDto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Ratings);
        if (!await _seriesService.UpdateRating(user, updateSeriesRatingDto)) return BadRequest("There was a critical error.");
        return Ok();
    }

    [HttpPost("update")]
    public async Task<ActionResult> UpdateSeries(UpdateSeriesDto updateSeries)
    {
        _logger.LogInformation("{UserName} is updating Series {SeriesName}", User.GetUsername(), updateSeries.Name);

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(updateSeries.Id);

        if (series == null) return BadRequest("Series does not exist");

        var seriesExists =
            await _unitOfWork.SeriesRepository.DoesSeriesNameExistInLibrary(updateSeries.Name.Trim(), series.LibraryId,
                series.Format);
        if (series.Name != updateSeries.Name && seriesExists)
        {
            return BadRequest("A series already exists in this library with this name. Series Names must be unique to a library.");
        }

        series.Name = updateSeries.Name.Trim();
        series.NormalizedName = Services.Tasks.Scanner.Parser.Parser.Normalize(series.Name);
        if (!string.IsNullOrEmpty(updateSeries.SortName.Trim()))
        {
            series.SortName = updateSeries.SortName.Trim();
        }

        series.LocalizedName = updateSeries.LocalizedName.Trim();
        series.NormalizedLocalizedName = Services.Tasks.Scanner.Parser.Parser.Normalize(series.LocalizedName);

        series.NameLocked = updateSeries.NameLocked;
        series.SortNameLocked = updateSeries.SortNameLocked;
        series.LocalizedNameLocked = updateSeries.LocalizedNameLocked;


        var needsRefreshMetadata = false;
        // This is when you hit Reset
        if (series.CoverImageLocked && !updateSeries.CoverImageLocked)
        {
            // Trigger a refresh when we are moving from a locked image to a non-locked
            needsRefreshMetadata = true;
            series.CoverImage = string.Empty;
            series.CoverImageLocked = updateSeries.CoverImageLocked;
        }

        _unitOfWork.SeriesRepository.Update(series);

        if (await _unitOfWork.CommitAsync())
        {
            if (needsRefreshMetadata)
            {
                _taskScheduler.RefreshSeriesMetadata(series.LibraryId, series.Id);
            }
            return Ok();
        }

        return BadRequest("There was an error with updating the series");
    }

    [HttpPost("recently-added")]
    public async Task<ActionResult<IEnumerable<SeriesDto>>> GetRecentlyAdded(FilterDto filterDto, [FromQuery] UserParams userParams, [FromQuery] int libraryId = 0)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
        var series =
            await _unitOfWork.SeriesRepository.GetRecentlyAdded(libraryId, userId, userParams, filterDto);

        // Apply progress/rating information (I can't work out how to do this in initial query)
        if (series == null) return BadRequest("Could not get series");

        await _unitOfWork.SeriesRepository.AddSeriesModifiers(userId, series);

        Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);

        return Ok(series);
    }

    [HttpPost("recently-updated-series")]
    public async Task<ActionResult<IEnumerable<RecentlyAddedItemDto>>> GetRecentlyAddedChapters()
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
        return Ok(await _unitOfWork.SeriesRepository.GetRecentlyUpdatedSeries(userId));
    }

    [HttpPost("all")]
    public async Task<ActionResult<IEnumerable<SeriesDto>>> GetAllSeries(FilterDto filterDto, [FromQuery] UserParams userParams, [FromQuery] int libraryId = 0)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
        var series =
            await _unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdAsync(libraryId, userId, userParams, filterDto);

        // Apply progress/rating information (I can't work out how to do this in initial query)
        if (series == null) return BadRequest("Could not get series");

        await _unitOfWork.SeriesRepository.AddSeriesModifiers(userId, series);

        Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);

        return Ok(series);
    }

    /// <summary>
    /// Fetches series that are on deck aka have progress on them.
    /// </summary>
    /// <param name="filterDto"></param>
    /// <param name="userParams"></param>
    /// <param name="libraryId">Default of 0 meaning all libraries</param>
    /// <returns></returns>
    [HttpPost("on-deck")]
    public async Task<ActionResult<IEnumerable<SeriesDto>>> GetOnDeck(FilterDto filterDto, [FromQuery] UserParams userParams, [FromQuery] int libraryId = 0)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
        var pagedList = await _unitOfWork.SeriesRepository.GetOnDeck(userId, libraryId, userParams, filterDto);

        await _unitOfWork.SeriesRepository.AddSeriesModifiers(userId, pagedList);

        Response.AddPaginationHeader(pagedList.CurrentPage, pagedList.PageSize, pagedList.TotalCount, pagedList.TotalPages);

        return Ok(pagedList);
    }

    /// <summary>
    /// Runs a Cover Image Generation task
    /// </summary>
    /// <param name="refreshSeriesDto"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("refresh-metadata")]
    public ActionResult RefreshSeriesMetadata(RefreshSeriesDto refreshSeriesDto)
    {
        _taskScheduler.RefreshSeriesMetadata(refreshSeriesDto.LibraryId, refreshSeriesDto.SeriesId, refreshSeriesDto.ForceUpdate);
        return Ok();
    }

    /// <summary>
    /// Scan a series and force each file to be updated. This should be invoked via the User, hence why we force.
    /// </summary>
    /// <param name="refreshSeriesDto"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("scan")]
    public ActionResult ScanSeries(RefreshSeriesDto refreshSeriesDto)
    {
        _taskScheduler.ScanSeries(refreshSeriesDto.LibraryId, refreshSeriesDto.SeriesId, refreshSeriesDto.ForceUpdate);
        return Ok();
    }

    /// <summary>
    /// Run a file analysis on the series.
    /// </summary>
    /// <param name="refreshSeriesDto"></param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("analyze")]
    public ActionResult AnalyzeSeries(RefreshSeriesDto refreshSeriesDto)
    {
        _taskScheduler.AnalyzeFilesForSeries(refreshSeriesDto.LibraryId, refreshSeriesDto.SeriesId, refreshSeriesDto.ForceUpdate);
        return Ok();
    }

    /// <summary>
    /// Returns metadata for a given series
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpGet("metadata")]
    public async Task<ActionResult<SeriesMetadataDto>> GetSeriesMetadata(int seriesId)
    {
        var metadata = await _unitOfWork.SeriesRepository.GetSeriesMetadata(seriesId);
        return Ok(metadata);
    }

    /// <summary>
    /// Update series metadata
    /// </summary>
    /// <param name="updateSeriesMetadataDto"></param>
    /// <returns></returns>
    [HttpPost("metadata")]
    public async Task<ActionResult> UpdateSeriesMetadata(UpdateSeriesMetadataDto updateSeriesMetadataDto)
    {
        if (await _seriesService.UpdateSeriesMetadata(updateSeriesMetadataDto))
        {
            return Ok("Successfully updated");
        }

        return BadRequest("Could not update metadata");
    }

    /// <summary>
    /// Returns all Series grouped by the passed Collection Id with Pagination.
    /// </summary>
    /// <param name="collectionId">Collection Id to pull series from</param>
    /// <param name="userParams">Pagination information</param>
    /// <returns></returns>
    [HttpGet("series-by-collection")]
    public async Task<ActionResult<IEnumerable<SeriesDto>>> GetSeriesByCollectionTag(int collectionId, [FromQuery] UserParams userParams)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
        var series =
            await _unitOfWork.SeriesRepository.GetSeriesDtoForCollectionAsync(collectionId, userId, userParams);

        // Apply progress/rating information (I can't work out how to do this in initial query)
        if (series == null) return BadRequest("Could not get series for collection");

        await _unitOfWork.SeriesRepository.AddSeriesModifiers(userId, series);

        Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);

        return Ok(series);
    }

    /// <summary>
    /// Fetches Series for a set of Ids. This will check User for permission access and filter out any Ids that don't exist or
    /// the user does not have access to.
    /// </summary>
    /// <returns></returns>
    [HttpPost("series-by-ids")]
    public async Task<ActionResult<IEnumerable<SeriesDto>>> GetAllSeriesById(SeriesByIdsDto dto)
    {
        if (dto.SeriesIds == null) return BadRequest("Must pass seriesIds");
        var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
        return Ok(await _unitOfWork.SeriesRepository.GetSeriesDtoForIdsAsync(dto.SeriesIds, userId));
    }

    /// <summary>
    /// Get the age rating for the <see cref="AgeRating"/> enum value
    /// </summary>
    /// <param name="ageRating"></param>
    /// <returns></returns>
    [HttpGet("age-rating")]
    public ActionResult<string> GetAgeRating(int ageRating)
    {
        var val = (AgeRating) ageRating;

        return Ok(val.ToDescription());
    }

    /// <summary>
    /// Get a special DTO for Series Detail page.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    /// <remarks>Do not rely on this API externally. May change without hesitation. </remarks>
    [HttpGet("series-detail")]
    public async Task<ActionResult<SeriesDetailDto>> GetSeriesDetailBreakdown(int seriesId)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
        return await _seriesService.GetSeriesDetail(seriesId, userId);
    }

    /// <summary>
    /// Returns the series for the MangaFile id. If the user does not have access (shouldn't happen by the UI),
    /// then null is returned
    /// </summary>
    /// <param name="mangaFileId"></param>
    /// <returns></returns>
    [HttpGet("series-for-mangafile")]
    public async Task<ActionResult<SeriesDto>> GetSeriesForMangaFile(int mangaFileId)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
        return Ok(await _unitOfWork.SeriesRepository.GetSeriesForMangaFile(mangaFileId, userId));
    }

    /// <summary>
    /// Returns the series for the Chapter id. If the user does not have access (shouldn't happen by the UI),
    /// then null is returned
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpGet("series-for-chapter")]
    public async Task<ActionResult<SeriesDto>> GetSeriesForChapter(int chapterId)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
        return Ok(await _unitOfWork.SeriesRepository.GetSeriesForChapter(chapterId, userId));
    }

    /// <summary>
    /// Fetches the related series for a given series
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="relation">Type of Relationship to pull back</param>
    /// <returns></returns>
    [HttpGet("related")]
    public async Task<ActionResult<IEnumerable<SeriesDto>>> GetRelatedSeries(int seriesId, RelationKind relation)
    {
        // Send back a custom DTO with each type or maybe sorted in some way
        var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
        return Ok(await _unitOfWork.SeriesRepository.GetSeriesForRelationKind(userId, seriesId, relation));
    }

    /// <summary>
    /// Returns all related series against the passed series Id
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpGet("all-related")]
    public async Task<ActionResult<RelatedSeriesDto>> GetAllRelatedSeries(int seriesId)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
        return Ok(await _unitOfWork.SeriesRepository.GetRelatedSeries(userId, seriesId));
    }


    /// <summary>
    /// Update the relations attached to the Series. Does not generate associated Sequel/Prequel pairs on target series.
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [Authorize(Policy="RequireAdminRole")]
    [HttpPost("update-related")]
    public async Task<ActionResult> UpdateRelatedSeries(UpdateRelatedSeriesDto dto)
    {
        if (await _seriesService.UpdateRelatedSeries(dto))
        {
            return Ok();
        }

        return BadRequest("There was an issue updating relationships");
    }

}
