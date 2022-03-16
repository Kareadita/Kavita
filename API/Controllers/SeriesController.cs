﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.Filtering;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using API.Services;
using Kavita.Common;
using Kavita.Common.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers
{
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

            if (series.Name != updateSeries.Name && await _unitOfWork.SeriesRepository.DoesSeriesNameExistInLibrary(updateSeries.Name, series.Format))
            {
                return BadRequest("A series already exists in this library with this name. Series Names must be unique to a library.");
            }

            series.Name = updateSeries.Name.Trim();
            series.SortName = updateSeries.SortName.Trim();
            series.LocalizedName = updateSeries.LocalizedName.Trim();

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

        [HttpPost("recently-added-chapters")]
        public async Task<ActionResult<IEnumerable<RecentlyAddedItemDto>>> GetRecentlyAddedChaptersAlt()
        {
            var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
            return Ok(await _unitOfWork.SeriesRepository.GetRecentlyAddedChapters(userId));
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
            // NOTE: This has to be done manually like this due to the DistinctBy requirement
            var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
            var results = await _unitOfWork.SeriesRepository.GetOnDeck(userId, libraryId, userParams, filterDto);

            var listResults = results.DistinctBy(s => s.Name).Skip((userParams.PageNumber - 1) * userParams.PageSize)
                .Take(userParams.PageSize).ToList();
            var pagedList = new PagedList<SeriesDto>(listResults, listResults.Count, userParams.PageNumber, userParams.PageSize);

            await _unitOfWork.SeriesRepository.AddSeriesModifiers(userId, pagedList);

            Response.AddPaginationHeader(pagedList.CurrentPage, pagedList.PageSize, pagedList.TotalCount, pagedList.TotalPages);

            return Ok(pagedList);
        }

        [Authorize(Policy = "RequireAdminRole")]
        [HttpPost("refresh-metadata")]
        public ActionResult RefreshSeriesMetadata(RefreshSeriesDto refreshSeriesDto)
        {
            _taskScheduler.RefreshSeriesMetadata(refreshSeriesDto.LibraryId, refreshSeriesDto.SeriesId, true);
            return Ok();
        }

        [Authorize(Policy = "RequireAdminRole")]
        [HttpPost("scan")]
        public ActionResult ScanSeries(RefreshSeriesDto refreshSeriesDto)
        {
            _taskScheduler.ScanSeries(refreshSeriesDto.LibraryId, refreshSeriesDto.SeriesId);
            return Ok();
        }

        [HttpGet("metadata")]
        public async Task<ActionResult<SeriesMetadataDto>> GetSeriesMetadata(int seriesId)
        {
            var metadata = await _unitOfWork.SeriesRepository.GetSeriesMetadata(seriesId);
            return Ok(metadata);
        }

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

        [HttpGet("age-rating")]
        public ActionResult<string> GetAgeRating(int ageRating)
        {
            var val = (AgeRating) ageRating;

            return Ok(val.ToDescription());
        }

        [HttpGet("series-detail")]
        public async Task<ActionResult<SeriesDetailDto>> GetSeriesDetailBreakdown(int seriesId)
        {
            var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
            return await _seriesService.GetSeriesDetail(seriesId, userId);
        }
    }
}
