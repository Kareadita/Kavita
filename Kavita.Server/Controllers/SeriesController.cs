using System;
using System.Collections.Generic;
using System.Threading;
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
using Kavita.Models.DTOs.Filtering.v2.Requests;
using Kavita.Models.DTOs.Metadata.Matching;
using Kavita.Models.DTOs.Recommendation;
using Kavita.Models.DTOs.KavitaPlus.ExternalMetadata;
using Kavita.Models.DTOs.SeriesDetail;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.MetadataMatching;
using Kavita.Server.Attributes;
using Kavita.Server.Extensions;
using Kavita.Server.Helpers;
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
    /// <param name="seriesFilterDto"></param>
    /// <returns></returns>
    [HttpPost("v2")]
    public async Task<ActionResult<PagedList<SeriesDto>>> GetSeriesForLibraryV2([FromQuery] UserParams userParams, [FromBody] SeriesFilterV2Dto seriesFilterDto)
    {
        var userId = UserId;
        var ct = HttpContext.RequestAborted;
        var series = await unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdAsync(userId, userParams, seriesFilterDto, ct: ct);

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
        var ct = HttpContext.RequestAborted;
        var series = await unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, UserId, ct);
        if (series == null) return NotFound();
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
        var ct = HttpContext.RequestAborted;
        logger.LogInformation("Series {SeriesId} is being deleted by {UserName}", seriesId, username.Sanitize());

        return Ok(await seriesService.DeleteMultipleSeries([seriesId], ct));
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
        var ct = HttpContext.RequestAborted;
        logger.LogInformation("Series {@SeriesId} is being deleted by {UserName}", dto.SeriesIds, username.Sanitize());

        if (await seriesService.DeleteMultipleSeries(dto.SeriesIds, ct)) return Ok(true);

        return BadRequest(await localizationService.TranslateAsync(UserId, "generic-series-delete"));
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
        var ct = HttpContext.RequestAborted;
        return Ok(await unitOfWork.VolumeRepository.GetVolumesDtoAsync(seriesId, UserId, ct: ct));
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
        var ct = HttpContext.RequestAborted;
        var vol = await unitOfWork.VolumeRepository.GetVolumeDtoAsync(volumeId, UserId, ct);
        if (vol == null) return NotFound();
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
        var ct = HttpContext.RequestAborted;
        var chapter = await unitOfWork.ChapterRepository.GetChapterDtoAsync(chapterId, UserId, ct);
        if (chapter == null) return NotFound();

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
        var ct = HttpContext.RequestAborted;
        var series = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(updateSeries.Id, ct: ct);
        if (series == null)
            return BadRequest(await localizationService.TranslateAsync(UserId, "series-doesnt-exist"));

        series.NormalizedName = series.Name.ToNormalized();
        if (!string.IsNullOrEmpty(updateSeries.SortName?.Trim()))
        {
            series.SortName = updateSeries.SortName.Trim();
        }

        series.LocalizedName = updateSeries.LocalizedName?.Trim();
        series.NormalizedLocalizedName = series.LocalizedName?.ToNormalized();

        series.SortNameLocked = updateSeries.SortNameLocked;
        series.LocalizedNameLocked = updateSeries.LocalizedNameLocked;

        ExternalMetadataIdHelper.SetExternalMetadataIds(series, updateSeries);


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

        if (!await unitOfWork.CommitAsync(ct))
        {
            return BadRequest(await localizationService.TranslateAsync(UserId, "generic-series-update"));
        }

        if (needsRefreshMetadata)
        {
            await taskScheduler.RefreshSeriesMetadata(series.LibraryId, series.Id);
        }

        return Ok(await unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(series.Id, UserId, ct));
    }

    /// <summary>
    /// Gets all recently added series
    /// </summary>
    /// <param name="seriesFilterDto"></param>
    /// <param name="userParams"></param>
    /// <returns></returns>
    [HttpPost("recently-added-v2")]
    public async Task<ActionResult<IEnumerable<SeriesDto>>> GetRecentlyAddedV2(SeriesFilterV2Dto seriesFilterDto, [FromQuery] UserParams userParams)
    {
        var userId = UserId;
        var ct = HttpContext.RequestAborted;
        var series =
            await unitOfWork.SeriesRepository.GetRecentlyAddedAsync(userId, userParams, seriesFilterDto, ct);

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
        var ct = HttpContext.RequestAborted;
        return Ok(await unitOfWork.SeriesRepository.GetRecentlyUpdatedSeriesAsync(UserId, userParams, ct));
    }

    /// <summary>
    /// Returns all series for the library
    /// </summary>
    /// <param name="seriesFilterDto"></param>
    /// <param name="userParams"></param>
    /// <param name="userId">Optional user id to request the OnDeck for someone else. They must have profile sharing enabled when doing so</param>
    /// <param name="context"></param>
    /// <returns></returns>
    [HttpPost("all-v2")]
    [ProfilePrivacy(allowMissingUserId: true)]
    public async Task<ActionResult<PagedList<SeriesDto>>> GetAllSeriesV2(SeriesFilterV2Dto seriesFilterDto, [FromQuery] UserParams userParams,
        [FromQuery] int? userId = null, [FromQuery] QueryContext context = QueryContext.None)
    {
        var ct = HttpContext.RequestAborted;
        var seriesForUser = userId ?? UserId;

        foreach (var stmt in await seriesService.GetProfilePrivacyStatements(seriesForUser, UserId, ct))
        {
            seriesFilterDto.Statements.Add(stmt);
        }

        var series = await unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdAsync(seriesForUser, userParams, seriesFilterDto, context, ct);

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
        var ct = HttpContext.RequestAborted;
        var pagedList = await unitOfWork.SeriesRepository.GetOnDeckAsync(UserId, libraryId, userParams, ct);

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
        var ct = HttpContext.RequestAborted;
        await unitOfWork.SeriesRepository.RemoveFromOnDeckAsync(seriesId, UserId, ct);
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
        var ct = HttpContext.RequestAborted;
        var pagedList = await seriesService.GetCurrentlyReading(userId, UserId, userParams, ct);

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
        var ct = HttpContext.RequestAborted;
        return Ok(await unitOfWork.SeriesRepository.GetSeriesMetadataAsync(seriesId, ct));
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
        var ct = HttpContext.RequestAborted;
        if (!await seriesService.UpdateSeriesMetadata(updateSeriesMetadataDto, ct))
            return BadRequest(await localizationService.TranslateAsync(UserId, "update-metadata-fail"));

        return Ok(await localizationService.TranslateAsync(UserId, "series-updated"));

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
        var ct = HttpContext.RequestAborted;
        var userId = UserId;
        var series =
            await unitOfWork.SeriesRepository.GetSeriesDtoForCollectionAsync(collectionId, userId, userParams, ct);

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
        var ct = HttpContext.RequestAborted;
        if (dto.SeriesIds == null) return BadRequest(await localizationService.TranslateAsync(UserId, "invalid-payload"));
        return Ok(await unitOfWork.SeriesRepository.GetSeriesDtoForIdsAsync(dto.SeriesIds, UserId, ct));
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
        var ct = HttpContext.RequestAborted;
        var val = (AgeRating) ageRating;
        // NOTE: Why not rename NotApplicable to NoRestriction and avoid this extra if?
        if (val == AgeRating.NotApplicable)
            return await localizationService.TranslateAsync(UserId, "age-restriction-not-applicable");

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
        var ct = HttpContext.RequestAborted;
        try
        {
            return await seriesService.GetSeriesDetail(seriesId, UserId, ct);
        }
        catch (KavitaException ex)
        {
            return BadRequest(await localizationService.TranslateAsync(UserId, ex.Message));
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
        var ct = HttpContext.RequestAborted;
        return Ok(await unitOfWork.SeriesRepository.GetSeriesForRelationKindAsync(UserId, seriesId, relation, ct));
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
        var ct = HttpContext.RequestAborted;
        return Ok(await seriesService.GetRelatedSeries(UserId, seriesId, ct));
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
        var ct = HttpContext.RequestAborted;
        if (await seriesService.UpdateRelatedSeries(dto, ct))
        {
            return Ok();
        }

        return BadRequest(await localizationService.TranslateAsync(UserId, "generic-relationship"));
    }

    [KPlus]
    [HttpGet("external-series-detail")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<ExternalSeriesDto>> GetExternalSeriesInfo(int? aniListId, long? malId, int? seriesId)
    {
        var ct = HttpContext.RequestAborted;
        var cacheKey = $"{CacheKey}-{aniListId ?? 0}-{malId ?? 0}-{seriesId ?? 0}";
        var results = await _externalSeriesCacheProvider.GetAsync<ExternalSeriesDto>(cacheKey, ct);
        if (results.HasValue)
        {
            return Ok(results.Value);
        }

        try
        {
            var ret = await externalMetadataService.GetExternalSeriesDetail(aniListId, malId, seriesId, ct);
            await _externalSeriesCacheProvider.SetAsync(cacheKey, ret, TimeSpan.FromMinutes(15), ct);
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
        var ct = HttpContext.RequestAborted;

        return Ok(await seriesService.GetEstimatedChapterCreationDate(seriesId, userId, ct));
    }

    /// <summary>
    /// Sends a request to Kavita+ API for all potential matches, sorted by relevance
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [KPlus]
    [HttpPost("match")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<IList<ExternalSeriesMatchDto>>> MatchSeries(MatchSeriesDto dto)
    {
        var ct = HttpContext.RequestAborted;
        var cacheKey = $"{MatchSeriesCacheKey}-{dto.SeriesId}-{dto.Query}";
        var results = await _matchSeriesCacheProvider.GetAsync<IList<ExternalSeriesMatchDto>>(cacheKey, ct);
        if (results.HasValue && !environment.IsDevelopment())
        {
            return Ok(results.Value);
        }

        var ret = await externalMetadataService.MatchSeries(dto, ct);
        await _matchSeriesCacheProvider.SetAsync(cacheKey, ret, TimeSpan.FromMinutes(1), ct);

        return Ok(ret);
    }

    /// <summary>
    /// This will perform the fix match
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="ids"></param>
    /// <returns></returns>
    [KPlus]
    [HttpPost("update-match")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public ActionResult UpdateSeriesMatch([FromQuery] int seriesId, [FromBody] ExternalMetadataIdsDto ids)
    {
        BackgroundJob.Enqueue(() => externalMetadataService.FixSeriesMatch(seriesId, ids, CancellationToken.None));

        return Ok();
    }

    /// <summary>
    /// When true, will not perform a match and will prevent Kavita from attempting to match/scrobble against this series
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="dontMatch"></param>
    /// <returns></returns>
    [KPlus]
    [HttpPost("dont-match")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult> UpdateDontMatch([FromQuery] int seriesId, [FromQuery] bool dontMatch)
    {
        var ct = HttpContext.RequestAborted;
        await externalMetadataService.UpdateSeriesDontMatch(seriesId, dontMatch, ct);
        return Ok();
    }

    /// <summary>
    /// Returns all Series that a user has access to
    /// </summary>
    /// <returns></returns>
    [HttpGet("series-with-annotations")]
    public async Task<ActionResult<IList<SeriesDto>>> GetSeriesWithAnnotations()
    {
        var ct = HttpContext.RequestAborted;
        return Ok(await unitOfWork.AnnotationRepository.GetSeriesWithAnnotations(UserId, ct));
    }


}
