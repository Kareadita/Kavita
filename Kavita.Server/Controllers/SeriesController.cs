using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EasyCaching.Core;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.Plus;
using Kavita.Common;
using Kavita.Common.Extensions;
using Kavita.Common.Helpers;
using Kavita.Models.Constants;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Dashboard;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.DTOs.Metadata.Matching;
using Kavita.Models.DTOs.Recommendation;
using Kavita.Models.DTOs.SeriesDetail;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.MetadataMatching;
using Kavita.Server.Attributes;
using Kavita.Server.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.Controllers;

public class SeriesController(
    ILogger<SeriesController> logger,
    ITaskScheduler taskScheduler,
    IUnitOfWork unitOfWork,
    ISeriesService seriesService,
    ILicenseService licenseService,
    IEasyCachingProviderFactory cachingProviderFactory,
    ILocalizationService localizationService,
    IExternalMetadataService externalMetadataService,
    IHostEnvironment environment)
    : BaseApiController
{
    private readonly IEasyCachingProvider _externalSeriesCacheProvider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.KavitaPlusExternalSeries);
    private readonly IEasyCachingProvider _matchSeriesCacheProvider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.KavitaPlusMatchSeries);
    private const string CacheKey = "externalSeriesData_";
    private const string MatchSeriesCacheKey = "matchSeries_";


    /// <summary>
    /// Gets series with the applied Filter
    /// </summary>
    /// <param name="userParams"></param>
    /// <param name="filterDto"></param>
    /// <returns></returns>
    [HttpPost("v2")]
    public async Task<ActionResult<PagedList<SeriesDto>>> GetSeriesForLibraryV2([FromQuery] UserParams userParams, [FromBody] FilterV2Dto filterDto)
    {
        var userId = UserId;
        var series =
            await unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdV2Async(userId, userParams, filterDto);

        Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);

        return Ok(series);
    }

    /// <summary>
    /// Fetches a Series for a given Id
    /// </summary>
    /// <param name="seriesId">Series Id to fetch details for</param>
    /// <returns></returns>
    /// <exception cref="NoContent">Throws an exception if the series Id does exist</exception>
    [SeriesAccess]
    [HttpGet("{seriesId:int}")]
    public async Task<ActionResult<SeriesDto>> GetSeries(int seriesId)
    {
        var series = await unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, UserId);
        if (series == null) return NoContent();
        return Ok(series);
    }

    /// <summary>
    /// Deletes a series from Kavita
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns>If the series was deleted or not</returns>
    [HttpDelete("{seriesId}")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<bool>> DeleteSeries(int seriesId)
    {
        var username = Username!;
        logger.LogInformation("Series {SeriesId} is being deleted by {UserName}", seriesId, username);

        return Ok(await seriesService.DeleteMultipleSeries([seriesId]));
    }

    /// <summary>
    /// Deletes multiple series from Kavita at once
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("delete-multiple")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult> DeleteMultipleSeries(DeleteSeriesDto dto)
    {
        var username = Username!;
        logger.LogInformation("Series {@SeriesId} is being deleted by {UserName}", dto.SeriesIds, username);

        if (await seriesService.DeleteMultipleSeries(dto.SeriesIds)) return Ok(true);

        return BadRequest(await localizationService.Translate(UserId, "generic-series-delete"));
    }

    /// <summary>
    /// Returns All volumes for a series with progress information and Chapters
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [SeriesAccess]
    [HttpGet("volumes")]
    public async Task<ActionResult<IEnumerable<VolumeDto>>> GetVolumes(int seriesId)
    {
        return Ok(await unitOfWork.VolumeRepository.GetVolumesDtoAsync(seriesId, UserId));
    }

    /// <summary>
    /// Returns a single Volume with progress information and Chapters
    /// </summary>
    /// <param name="volumeId"></param>
    /// <returns></returns>
    [VolumeAccess]
    [HttpGet("volume")]
    public async Task<ActionResult<VolumeDto?>> GetVolume(int volumeId)
    {
        var vol = await unitOfWork.VolumeRepository.GetVolumeDtoAsync(volumeId, UserId);
        if (vol == null) return NoContent();
        return Ok(vol);
    }

    /// <summary>
    /// Returns a single Chapter with progress information
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [ChapterAccess]
    [HttpGet("chapter")]
    public async Task<ActionResult<ChapterDto>> GetChapter(int chapterId)
    {
        var chapter = await unitOfWork.ChapterRepository.GetChapterDtoAsync(chapterId, UserId);
        if (chapter == null) return NoContent();

        return Ok(chapter);
    }

    /// <summary>
    /// Updates the Series
    /// </summary>
    /// <param name="updateSeries"></param>
    /// <returns>Updated Series</returns>
    [HttpPost("update")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<SeriesDto>> UpdateSeries(UpdateSeriesDto updateSeries)
    {
        var series = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(updateSeries.Id);
        if (series == null)
            return BadRequest(await localizationService.Translate(UserId, "series-doesnt-exist"));

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
            series.CoverImage = null;
            series.CoverImageLocked = false;
            series.Metadata.KPlusOverrides.Remove(MetadataSettingField.Covers);
            logger.LogDebug("[SeriesCoverImageBug] Setting Series Cover Image to null: {SeriesId}", series.Id);
            series.ResetColorScape();

        }

        unitOfWork.SeriesRepository.Update(series);

        if (!await unitOfWork.CommitAsync())
        {
            return BadRequest(await localizationService.Translate(UserId, "generic-series-update"));
        }

        if (needsRefreshMetadata)
        {
            await taskScheduler.RefreshSeriesMetadata(series.LibraryId, series.Id);
        }

        return Ok(await unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(series.Id, UserId));
    }

    /// <summary>
    /// Gets all recently added series
    /// </summary>
    /// <param name="filterDto"></param>
    /// <param name="userParams"></param>
    /// <returns></returns>
    [HttpPost("recently-added-v2")]
    public async Task<ActionResult<IEnumerable<SeriesDto>>> GetRecentlyAddedV2(FilterV2Dto filterDto, [FromQuery] UserParams userParams)
    {
        var userId = UserId;
        var series =
            await unitOfWork.SeriesRepository.GetRecentlyAddedV2(userId, userParams, filterDto);

        Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);

        return Ok(series);
    }

    /// <summary>
    /// Returns series that were recently updated, like adding or removing a chapter
    /// </summary>
    /// <param name="userParams">Page size and offset</param>
    /// <returns></returns>
    [HttpPost("recently-updated-series")]
    public async Task<ActionResult<IList<GroupedSeriesDto>>> GetRecentlyAddedChapters([FromQuery] UserParams? userParams)
    {
        userParams ??= UserParams.Default;
        return Ok(await unitOfWork.SeriesRepository.GetRecentlyUpdatedSeries(UserId, userParams));
    }

    /// <summary>
    /// Returns all series for the library
    /// </summary>
    /// <param name="filterDto"></param>
    /// <param name="userParams"></param>
    /// <param name="userId">Optional user id to request the OnDeck for someone else. They must have profile sharing enabled when doing so</param>
    /// <param name="libraryId">This is not in use</param>
    /// <param name="context"></param>
    /// <returns></returns>
    [HttpPost("all-v2")]
    [ProfilePrivacy(allowMissingUserId: true)]
    public async Task<ActionResult<PagedList<SeriesDto>>> GetAllSeriesV2(FilterV2Dto filterDto, [FromQuery] UserParams userParams,
        [FromQuery] int? userId = null, [FromQuery] int libraryId = 0, [FromQuery] QueryContext context = QueryContext.None)
    {
        var seriesForUser = userId ?? UserId;

        filterDto.Statements.AddRange(await seriesService.GetProfilePrivacyStatements(seriesForUser, UserId));

        var series =
            await unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdV2Async(seriesForUser, userParams, filterDto, context);

        Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);

        return Ok(series);
    }


    /// <summary>
    /// Fetches series that are on deck aka have progress on them.
    /// </summary>
    /// <param name="userParams"></param>
    /// <param name="libraryId">Default of 0 meaning all libraries</param>
    /// <returns></returns>
    [HttpPost("on-deck")]
    public async Task<ActionResult<PagedList<SeriesDto>>> GetOnDeck([FromQuery] UserParams userParams, [FromQuery] int libraryId = 0)
    {
        var pagedList = await unitOfWork.SeriesRepository.GetOnDeck(UserId, libraryId, userParams, null);

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
        await unitOfWork.SeriesRepository.RemoveFromOnDeck(seriesId, UserId);
        return Ok();
    }

    /// <summary>
    /// Get series a user is currently reading, requires the user to share their profile
    /// </summary>
    /// <param name="userParams"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    [ProfilePrivacy]
    [HttpGet("currently-reading")]
    public async Task<ActionResult<PagedList<SeriesDto>>> GetCurrentlyReadingForUser([FromQuery] UserParams userParams, [FromQuery] int userId)
    {
        var pagedList = await seriesService.GetCurrentlyReading(userId, UserId, userParams);

        Response.AddPaginationHeader(pagedList.CurrentPage, pagedList.PageSize, pagedList.TotalCount, pagedList.TotalPages);

        return Ok(pagedList);
    }


    /// <summary>
    /// Runs a Cover Image Generation task
    /// </summary>
    /// <param name="refreshSeriesDto"></param>
    /// <returns></returns>
    [HttpPost("refresh-metadata")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult> RefreshSeriesMetadata(RefreshSeriesDto refreshSeriesDto)
    {
        await taskScheduler.RefreshSeriesMetadata(refreshSeriesDto.LibraryId, refreshSeriesDto.SeriesId, refreshSeriesDto.ForceUpdate, refreshSeriesDto.ForceColorscape);
        return Ok();
    }

    /// <summary>
    /// Scan a series and force each file to be updated. This should be invoked via the User, hence why we force.
    /// </summary>
    /// <param name="refreshSeriesDto"></param>
    /// <returns></returns>
    [HttpPost("scan")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public ActionResult ScanSeries(RefreshSeriesDto refreshSeriesDto)
    {
        taskScheduler.ScanSeries(refreshSeriesDto.LibraryId, refreshSeriesDto.SeriesId, true);
        return Ok();
    }

    /// <summary>
    /// Run a file analysis on the series.
    /// </summary>
    /// <param name="refreshSeriesDto"></param>
    /// <returns></returns>
    [HttpPost("analyze")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public ActionResult AnalyzeSeries(RefreshSeriesDto refreshSeriesDto)
    {
        taskScheduler.AnalyzeFilesForSeries(refreshSeriesDto.LibraryId, refreshSeriesDto.SeriesId, refreshSeriesDto.ForceUpdate);
        return Ok();
    }

    /// <summary>
    /// Returns metadata for a given series
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [SeriesAccess]
    [HttpGet("metadata")]
    public async Task<ActionResult<SeriesMetadataDto>> GetSeriesMetadata(int seriesId)
    {
        return Ok(await unitOfWork.SeriesRepository.GetSeriesMetadata(seriesId));
    }

    /// <summary>
    /// Update series metadata
    /// </summary>
    /// <param name="updateSeriesMetadataDto"></param>
    /// <returns></returns>
    [HttpPost("metadata")]
    [Authorize(PolicyGroups.AdminPolicy)]
    public async Task<ActionResult> UpdateSeriesMetadata(UpdateSeriesMetadataDto updateSeriesMetadataDto)
    {
        if (!await seriesService.UpdateSeriesMetadata(updateSeriesMetadataDto))
            return BadRequest(await localizationService.Translate(UserId, "update-metadata-fail"));

        return Ok(await localizationService.Translate(UserId, "series-updated"));

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
        var userId = UserId;
        var series =
            await unitOfWork.SeriesRepository.GetSeriesDtoForCollectionAsync(collectionId, userId, userParams);

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
        if (dto.SeriesIds == null) return BadRequest(await localizationService.Translate(UserId, "invalid-payload"));
        return Ok(await unitOfWork.SeriesRepository.GetSeriesDtoForIdsAsync(dto.SeriesIds, UserId));
    }

    /// <summary>
    /// Get the age rating for the <see cref="AgeRating"/> enum value
    /// </summary>
    /// <param name="ageRating"></param>
    /// <returns></returns>
    [HttpGet("age-rating")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Month, VaryByQueryKeys = ["ageRating"])]
    public async Task<ActionResult<string>> GetAgeRating(int ageRating)
    {
        var val = (AgeRating) ageRating;
        if (val == AgeRating.NotApplicable)
            return await localizationService.Translate(UserId, "age-restriction-not-applicable");

        return Ok(val.ToDescription());
    }

    /// <summary>
    /// Get a special DTO for Series Detail page.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    /// <remarks>Do not rely on this API externally. May change without hesitation. </remarks>
    [SeriesAccess]
    [HttpGet("series-detail")]
    public async Task<ActionResult<SeriesDetailDto>> GetSeriesDetailBreakdown(int seriesId)
    {
        try
        {
            return await seriesService.GetSeriesDetail(seriesId, UserId);
        }
        catch (KavitaException ex)
        {
            return BadRequest(await localizationService.Translate(UserId, ex.Message));
        }
    }



    /// <summary>
    /// Fetches the related series for a given series
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="relation">Type of Relationship to pull back</param>
    /// <returns></returns>
    [SeriesAccess]
    [HttpGet("related")]
    public async Task<ActionResult<IEnumerable<SeriesDto>>> GetRelatedSeries(int seriesId, RelationKind relation)
    {
        return Ok(await unitOfWork.SeriesRepository.GetSeriesForRelationKind(UserId, seriesId, relation));
    }

    /// <summary>
    /// Returns all related series against the passed series Id
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [SeriesAccess]
    [HttpGet("all-related")]
    public async Task<ActionResult<RelatedSeriesDto>> GetAllRelatedSeries(int seriesId)
    {
        return Ok(await seriesService.GetRelatedSeries(UserId, seriesId));
    }


    /// <summary>
    /// Update the relations attached to the Series. Does not generate associated Sequel/Prequel pairs on target series.
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update-related")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult> UpdateRelatedSeries(UpdateRelatedSeriesDto dto)
    {
        if (await seriesService.UpdateRelatedSeries(dto))
        {
            return Ok();
        }

        return BadRequest(await localizationService.Translate(UserId, "generic-relationship"));
    }

    [KPlus]
    [HttpGet("external-series-detail")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<ExternalSeriesDto>> GetExternalSeriesInfo(int? aniListId, long? malId, int? seriesId)
    {
        var cacheKey = $"{CacheKey}-{aniListId ?? 0}-{malId ?? 0}-{seriesId ?? 0}";
        var results = await _externalSeriesCacheProvider.GetAsync<ExternalSeriesDto>(cacheKey);
        if (results.HasValue)
        {
            return Ok(results.Value);
        }

        try
        {
            var ret = await externalMetadataService.GetExternalSeriesDetail(aniListId, malId, seriesId);
            await _externalSeriesCacheProvider.SetAsync(cacheKey, ret, TimeSpan.FromMinutes(15));
            return Ok(ret);
        }
        catch (Exception)
        {
            return BadRequest("Unable to load External Series details");
        }
    }

    /// <summary>
    /// Based on the delta times between when chapters are added, for series that are not Completed/Cancelled/Hiatus, forecast the next
    /// date when it will be available.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [SeriesAccess]
    [HttpGet("next-expected")]
    public async Task<ActionResult<NextExpectedChapterDto>> GetNextExpectedChapter(int seriesId)
    {
        var userId = UserId;

        return Ok(await seriesService.GetEstimatedChapterCreationDate(seriesId, userId));
    }

    /// <summary>
    /// Sends a request to Kavita+ API for all potential matches, sorted by relevance
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("match")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<IList<ExternalSeriesMatchDto>>> MatchSeries(MatchSeriesDto dto)
    {
        var cacheKey = $"{MatchSeriesCacheKey}-{dto.SeriesId}-{dto.Query}";
        var results = await _matchSeriesCacheProvider.GetAsync<IList<ExternalSeriesMatchDto>>(cacheKey);
        if (results.HasValue && !environment.IsDevelopment())
        {
            return Ok(results.Value);
        }

        var ret = await externalMetadataService.MatchSeries(dto);
        await _matchSeriesCacheProvider.SetAsync(cacheKey, ret, TimeSpan.FromMinutes(1));

        return Ok(ret);
    }

    /// <summary>
    /// This will perform the fix match
    /// </summary>
    /// <param name="match"></param>
    /// <param name="seriesId"></param>
    /// <param name="aniListId"></param>
    /// <param name="malId"></param>
    /// <param name="cbrId"></param>
    /// <returns></returns>
    [HttpPost("update-match")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public ActionResult UpdateSeriesMatch([FromQuery] int seriesId, [FromQuery] int? aniListId, [FromQuery] long? malId, [FromQuery] int? cbrId)
    {
        BackgroundJob.Enqueue(() => externalMetadataService.FixSeriesMatch(seriesId, aniListId, malId, cbrId));

        return Ok();
    }

    /// <summary>
    /// When true, will not perform a match and will prevent Kavita from attempting to match/scrobble against this series
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="dontMatch"></param>
    /// <returns></returns>
    [HttpPost("dont-match")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult> UpdateDontMatch([FromQuery] int seriesId, [FromQuery] bool dontMatch)
    {
        await externalMetadataService.UpdateSeriesDontMatch(seriesId, dontMatch);
        return Ok();
    }

    /// <summary>
    /// Returns all Series that a user has access to
    /// </summary>
    /// <returns></returns>
    [HttpGet("series-with-annotations")]
    public async Task<ActionResult<IList<SeriesDto>>> GetSeriesWithAnnotations()
    {
        var data = await unitOfWork.AnnotationRepository.GetSeriesWithAnnotations(UserId);
        return Ok(data);
    }


}
