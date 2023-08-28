using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.Filtering;
using API.DTOs.Filtering.v2;
using API.DTOs.Metadata;
using API.DTOs.SeriesDetail;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using API.Services;
using API.Services.Plus;
using EasyCaching.Core;
using Kavita.Common;
using Kavita.Common.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers;

public class SeriesController : BaseApiController
{
    private readonly ILogger<SeriesController> _logger;
    private readonly ITaskScheduler _taskScheduler;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISeriesService _seriesService;
    private readonly ILicenseService _licenseService;
    private readonly ILocalizationService _localizationService;
    private readonly IEasyCachingProvider _ratingCacheProvider;
    private readonly IEasyCachingProvider _reviewCacheProvider;
    private readonly IEasyCachingProvider _recommendationCacheProvider;


    public SeriesController(ILogger<SeriesController> logger, ITaskScheduler taskScheduler, IUnitOfWork unitOfWork,
        ISeriesService seriesService, ILicenseService licenseService,
        IEasyCachingProviderFactory cachingProviderFactory, ILocalizationService localizationService)
    {
        _logger = logger;
        _taskScheduler = taskScheduler;
        _unitOfWork = unitOfWork;
        _seriesService = seriesService;
        _licenseService = licenseService;
        _localizationService = localizationService;

        _ratingCacheProvider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.KavitaPlusRatings);
        _reviewCacheProvider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.KavitaPlusReviews);
        _recommendationCacheProvider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.KavitaPlusRecommendations);
    }

    /// <summary>
    /// Gets series with the applied Filter
    /// </summary>
    /// <remarks>This is considered v1 and no longer used by Kavita, but will be supported for sometime. See series/v2</remarks>
    /// <param name="libraryId"></param>
    /// <param name="userParams"></param>
    /// <param name="filterDto"></param>
    /// <returns></returns>
    [HttpPost]
    [Obsolete("use v2")]
    public async Task<ActionResult<IEnumerable<Series>>> GetSeriesForLibrary(int libraryId, [FromQuery] UserParams userParams, [FromBody] FilterDto filterDto)
    {
        var userId = User.GetUserId();
        var series =
            await _unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdAsync(libraryId, userId, userParams, filterDto);

        // Apply progress/rating information (I can't work out how to do this in initial query)
        if (series == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "no-series"));

        await _unitOfWork.SeriesRepository.AddSeriesModifiers(userId, series);

        Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);

        return Ok(series);
    }

    /// <summary>
    /// Gets series with the applied Filter
    /// </summary>
    /// <param name="userParams"></param>
    /// <param name="filterDto"></param>
    /// <returns></returns>
    [HttpPost("v2")]
    public async Task<ActionResult<IEnumerable<Series>>> GetSeriesForLibraryV2([FromQuery] UserParams userParams, [FromBody] FilterV2Dto filterDto)
    {
        var userId = User.GetUserId();
        var series =
            await _unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdV2Async(userId, userParams, filterDto);

        //TODO: We might want something like libraryId as source so that I don't have to muck with the groups

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
    /// <exception cref="NoContent">Throws an exception if the series Id does exist</exception>
    [HttpGet("{seriesId:int}")]
    public async Task<ActionResult<SeriesDto>> GetSeries(int seriesId)
    {
        var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, User.GetUserId());
        if (series == null) return NoContent();
        return Ok(series);
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

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-series-delete"));
    }

    /// <summary>
    /// Returns All volumes for a series with progress information and Chapters
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpGet("volumes")]
    public async Task<ActionResult<IEnumerable<VolumeDto>>> GetVolumes(int seriesId)
    {
        return Ok(await _unitOfWork.VolumeRepository.GetVolumesDtoAsync(seriesId, User.GetUserId()));
    }

    [HttpGet("volume")]
    public async Task<ActionResult<VolumeDto?>> GetVolume(int volumeId)
    {
        var vol = await _unitOfWork.VolumeRepository.GetVolumeDtoAsync(volumeId, User.GetUserId());
        if (vol == null) return NoContent();
        return Ok(vol);
    }

    [HttpGet("chapter")]
    public async Task<ActionResult<ChapterDto>> GetChapter(int chapterId)
    {
        var chapter = await _unitOfWork.ChapterRepository.GetChapterDtoAsync(chapterId);
        if (chapter == null) return NoContent();
        return Ok(await _unitOfWork.ChapterRepository.AddChapterModifiers(User.GetUserId(), chapter));
    }

    [HttpGet("chapter-metadata")]
    public async Task<ActionResult<ChapterMetadataDto>> GetChapterMetadata(int chapterId)
    {
        return Ok(await _unitOfWork.ChapterRepository.GetChapterMetadataDtoAsync(chapterId));
    }


    /// <summary>
    /// Update the user rating for the given series
    /// </summary>
    /// <param name="updateSeriesRatingDto"></param>
    /// <returns></returns>
    [HttpPost("update-rating")]
    public async Task<ActionResult> UpdateSeriesRating(UpdateSeriesRatingDto updateSeriesRatingDto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Ratings);
        if (!await _seriesService.UpdateRating(user!, updateSeriesRatingDto))
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-error"));
        return Ok();
    }

    /// <summary>
    /// Updates the Series
    /// </summary>
    /// <param name="updateSeries"></param>
    /// <returns></returns>
    [HttpPost("update")]
    public async Task<ActionResult> UpdateSeries(UpdateSeriesDto updateSeries)
    {
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(updateSeries.Id);
        if (series == null)
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "series-doesnt-exist"));

        series.NormalizedName = series.Name.ToNormalized();
        if (!string.IsNullOrEmpty(updateSeries.SortName?.Trim()))
        {
            series.SortName = updateSeries.SortName.Trim();
        }

        series.LocalizedName = updateSeries.LocalizedName?.Trim();
        series.NormalizedLocalizedName = series.LocalizedName?.ToNormalized();

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

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-series-update"));
    }

    /// <summary>
    /// Gets all recently added series. Obsolete, use recently-added-v2
    /// </summary>
    /// <param name="filterDto"></param>
    /// <param name="userParams"></param>
    /// <param name="libraryId"></param>
    /// <returns></returns>
    [ResponseCache(CacheProfileName = "Instant")]
    [HttpPost("recently-added")]
    [Obsolete("use recently-added-v2")]
    public async Task<ActionResult<IEnumerable<SeriesDto>>> GetRecentlyAdded(FilterDto filterDto, [FromQuery] UserParams userParams, [FromQuery] int libraryId = 0)
    {
        var userId = User.GetUserId();
        var series =
            await _unitOfWork.SeriesRepository.GetRecentlyAdded(libraryId, userId, userParams, filterDto);

        // Apply progress/rating information (I can't work out how to do this in initial query)
        if (series == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "no-series"));

        await _unitOfWork.SeriesRepository.AddSeriesModifiers(userId, series);

        Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);

        return Ok(series);
    }

    /// <summary>
    /// Gets all recently added series
    /// </summary>
    /// <param name="filterDto"></param>
    /// <param name="userParams"></param>
    /// <returns></returns>
    [ResponseCache(CacheProfileName = "Instant")]
    [HttpPost("recently-added-v2")]
    public async Task<ActionResult<IEnumerable<SeriesDto>>> GetRecentlyAddedV2(FilterV2Dto filterDto, [FromQuery] UserParams userParams)
    {
        var userId = User.GetUserId();
        var series =
            await _unitOfWork.SeriesRepository.GetRecentlyAddedV2(userId, userParams, filterDto);

        // Apply progress/rating information (I can't work out how to do this in initial query)
        if (series == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "no-series"));

        await _unitOfWork.SeriesRepository.AddSeriesModifiers(userId, series);

        Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);

        return Ok(series);
    }

    /// <summary>
    /// Returns series that were recently updated, like adding or removing a chapter
    /// </summary>
    /// <returns></returns>
    [ResponseCache(CacheProfileName = "Instant")]
    [HttpPost("recently-updated-series")]
    public async Task<ActionResult<IEnumerable<RecentlyAddedItemDto>>> GetRecentlyAddedChapters()
    {
        return Ok(await _unitOfWork.SeriesRepository.GetRecentlyUpdatedSeries(User.GetUserId(), 20));
    }

    /// <summary>
    /// Returns all series for the library
    /// </summary>
    /// <param name="filterDto"></param>
    /// <param name="userParams"></param>
    /// <param name="libraryId"></param>
    /// <returns></returns>
    [HttpPost("all")]
    public async Task<ActionResult<IEnumerable<SeriesDto>>> GetAllSeries(FilterV2Dto filterDto, [FromQuery] UserParams userParams, [FromQuery] int libraryId = 0)
    {
        var userId = User.GetUserId();
        var series =
            await _unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdV2Async(userId, userParams, filterDto);

        // Apply progress/rating information (I can't work out how to do this in initial query)
        if (series == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "no-series"));

        await _unitOfWork.SeriesRepository.AddSeriesModifiers(userId, series);

        Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);

        return Ok(series);
    }

    /// <summary>
    /// Fetches series that are on deck aka have progress on them.
    /// </summary>
    /// <param name="userParams"></param>
    /// <param name="libraryId">Default of 0 meaning all libraries</param>
    /// <returns></returns>
    [ResponseCache(CacheProfileName = "Instant")]
    [HttpPost("on-deck")]
    public async Task<ActionResult<IEnumerable<SeriesDto>>> GetOnDeck([FromQuery] UserParams userParams, [FromQuery] int libraryId = 0)
    {
        var userId = User.GetUserId();
        var pagedList = await _unitOfWork.SeriesRepository.GetOnDeck(userId, libraryId, userParams, null);

        await _unitOfWork.SeriesRepository.AddSeriesModifiers(userId, pagedList);

        Response.AddPaginationHeader(pagedList.CurrentPage, pagedList.PageSize, pagedList.TotalCount, pagedList.TotalPages);

        return Ok(pagedList);
    }


    /// <summary>
    /// Removes a series from displaying on deck until the next read event on that series
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpPost("remove-from-on-deck")]
    public async Task<ActionResult> RemoveFromOnDeck([FromQuery] int seriesId)
    {
        await _unitOfWork.SeriesRepository.RemoveFromOnDeck(seriesId, User.GetUserId());
        return Ok();
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
        _taskScheduler.ScanSeries(refreshSeriesDto.LibraryId, refreshSeriesDto.SeriesId, true);
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
            if (await _licenseService.HasActiveLicense())
            {
                _logger.LogDebug("Clearing cache as series weblinks may have changed");
                await _reviewCacheProvider.RemoveAsync(ReviewController.CacheKey + updateSeriesMetadataDto.SeriesMetadata.SeriesId);
                await _ratingCacheProvider.RemoveAsync(RatingController.CacheKey + updateSeriesMetadataDto.SeriesMetadata.SeriesId);

                var allUsers = (await _unitOfWork.UserRepository.GetAllUsersAsync()).Select(s => s.Id);
                foreach (var userId in allUsers)
                {
                    await _recommendationCacheProvider.RemoveAsync(RecommendedController.CacheKey + $"{updateSeriesMetadataDto.SeriesMetadata.SeriesId}-{userId}");
                }
            }

            return Ok(await _localizationService.Translate(User.GetUserId(), "series-updated"));
        }

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "update-metadata-fail"));
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
        var userId = User.GetUserId();
        var series =
            await _unitOfWork.SeriesRepository.GetSeriesDtoForCollectionAsync(collectionId, userId, userParams);

        // Apply progress/rating information (I can't work out how to do this in initial query)
        if (series == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "no-series-collection"));

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
        if (dto.SeriesIds == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "invalid-payload"));
        return Ok(await _unitOfWork.SeriesRepository.GetSeriesDtoForIdsAsync(dto.SeriesIds, User.GetUserId()));
    }

    /// <summary>
    /// Get the age rating for the <see cref="AgeRating"/> enum value
    /// </summary>
    /// <param name="ageRating"></param>
    /// <returns></returns>
    /// <remarks>This is cached for an hour</remarks>
    [ResponseCache(CacheProfileName = "Month", VaryByQueryKeys = new [] {"ageRating"})]
    [HttpGet("age-rating")]
    public async Task<ActionResult<string>> GetAgeRating(int ageRating)
    {
        var val = (AgeRating) ageRating;
        if (val == AgeRating.NotApplicable)
            return await _localizationService.Translate(User.GetUserId(), "age-restriction-not-applicable");

        return Ok(val.ToDescription());
    }

    /// <summary>
    /// Get a special DTO for Series Detail page.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    /// <remarks>Do not rely on this API externally. May change without hesitation. </remarks>
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.FiveMinute, VaryByQueryKeys = new [] {"seriesId"})]
    [HttpGet("series-detail")]
    public async Task<ActionResult<SeriesDetailDto>> GetSeriesDetailBreakdown(int seriesId)
    {
        try
        {
            return await _seriesService.GetSeriesDetail(seriesId, User.GetUserId());
        }
        catch (KavitaException ex)
        {
            return BadRequest(await _localizationService.Translate(User.GetUserId(), ex.Message));
        }
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
        return Ok(await _unitOfWork.SeriesRepository.GetSeriesForRelationKind(User.GetUserId(), seriesId, relation));
    }

    /// <summary>
    /// Returns all related series against the passed series Id
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpGet("all-related")]
    public async Task<ActionResult<RelatedSeriesDto>> GetAllRelatedSeries(int seriesId)
    {
        return Ok(await _seriesService.GetRelatedSeries(User.GetUserId(), seriesId));
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

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-relationship"));
    }



}
