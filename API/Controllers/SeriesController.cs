using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.Filtering;
using API.Entities;
using API.Extensions;
using API.Helpers;
using API.Interfaces;
using API.SignalR;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace API.Controllers
{
    public class SeriesController : BaseApiController
    {
        private readonly ILogger<SeriesController> _logger;
        private readonly ITaskScheduler _taskScheduler;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHubContext<MessageHub> _messageHub;

        public SeriesController(ILogger<SeriesController> logger, ITaskScheduler taskScheduler, IUnitOfWork unitOfWork, IHubContext<MessageHub> messageHub)
        {
            _logger = logger;
            _taskScheduler = taskScheduler;
            _unitOfWork = unitOfWork;
            _messageHub = messageHub;
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
        [HttpGet("{seriesId}")]
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

            var chapterIds = (await _unitOfWork.SeriesRepository.GetChapterIdsForSeriesAsync(new []{seriesId}));
            var result = await _unitOfWork.SeriesRepository.DeleteSeriesAsync(seriesId);

            if (result)
            {
                await _unitOfWork.AppUserProgressRepository.CleanupAbandonedChapters();
                await _unitOfWork.CollectionTagRepository.RemoveTagsWithoutSeries();
                await _unitOfWork.CommitAsync();
                _taskScheduler.CleanupChapters(chapterIds);
            }
            return Ok(result);
        }

        [Authorize(Policy = "RequireAdminRole")]
        [HttpPost("delete-multiple")]
        public async Task<ActionResult> DeleteMultipleSeries(DeleteSeriesDto dto)
        {
            var username = User.GetUsername();
            _logger.LogInformation("Series {SeriesId} is being deleted by {UserName}", dto.SeriesIds, username);

            var chapterMappings =
                await _unitOfWork.SeriesRepository.GetChapterIdWithSeriesIdForSeriesAsync(dto.SeriesIds.ToArray());

            var allChapterIds = new List<int>();
            foreach (var mapping in chapterMappings)
            {
                allChapterIds.AddRange(mapping.Value);
            }

            var series = await _unitOfWork.SeriesRepository.GetSeriesByIdsAsync(dto.SeriesIds);
            _unitOfWork.SeriesRepository.Remove(series);

            if (_unitOfWork.HasChanges() && await _unitOfWork.CommitAsync())
            {
                await _unitOfWork.AppUserProgressRepository.CleanupAbandonedChapters();
                await _unitOfWork.CollectionTagRepository.RemoveTagsWithoutSeries();
                _taskScheduler.CleanupChapters(allChapterIds.ToArray());
            }
            return Ok();
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
        public async Task<ActionResult<VolumeDto>> GetChapter(int chapterId)
        {
            return Ok(await _unitOfWork.ChapterRepository.GetChapterDtoAsync(chapterId));
        }


        [HttpPost("update-rating")]
        public async Task<ActionResult> UpdateSeriesRating(UpdateSeriesRatingDto updateSeriesRatingDto)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Ratings);
            var userRating = await _unitOfWork.UserRepository.GetUserRating(updateSeriesRatingDto.SeriesId, user.Id) ??
                             new AppUserRating();

            userRating.Rating = updateSeriesRatingDto.UserRating;
            userRating.Review = updateSeriesRatingDto.UserReview;
            userRating.SeriesId = updateSeriesRatingDto.SeriesId;

            if (userRating.Id == 0)
            {
                user.Ratings ??= new List<AppUserRating>();
                user.Ratings.Add(userRating);
            }

            _unitOfWork.UserRepository.Update(user);

            if (!await _unitOfWork.CommitAsync()) return BadRequest("There was a critical error.");

            return Ok();
        }

        [HttpPost("update")]
        public async Task<ActionResult> UpdateSeries(UpdateSeriesDto updateSeries)
        {
            _logger.LogInformation("{UserName} is updating Series {SeriesName}", User.GetUsername(), updateSeries.Name);

            var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(updateSeries.Id);

            if (series == null) return BadRequest("Series does not exist");

            if (series.Name != updateSeries.Name && await _unitOfWork.SeriesRepository.DoesSeriesNameExistInLibrary(updateSeries.Name))
            {
                return BadRequest("A series already exists in this library with this name. Series Names must be unique to a library.");
            }
            series.Name = updateSeries.Name.Trim();
            series.LocalizedName = updateSeries.LocalizedName.Trim();
            series.SortName = updateSeries.SortName?.Trim();
            series.Summary = updateSeries.Summary?.Trim();

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

        [HttpPost("in-progress")]
        public async Task<ActionResult<IEnumerable<SeriesDto>>> GetInProgress(FilterDto filterDto, [FromQuery] UserParams userParams, [FromQuery] int libraryId = 0)
        {
            // NOTE: This has to be done manually like this due to the DistinctBy requirement
            var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
            var results = await _unitOfWork.SeriesRepository.GetInProgress(userId, libraryId, userParams, filterDto);

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
            try
            {
                var seriesId = updateSeriesMetadataDto.SeriesMetadata.SeriesId;
                var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId);
                var allTags = (await _unitOfWork.CollectionTagRepository.GetAllTagsAsync()).ToList();
                if (series.Metadata == null)
                {
                    series.Metadata = DbFactory.SeriesMetadata(updateSeriesMetadataDto.Tags
                        .Select(dto => DbFactory.CollectionTag(dto.Id, dto.Title, dto.Summary, dto.Promoted)).ToList());
                }
                else
                {
                    series.Metadata.CollectionTags ??= new List<CollectionTag>();
                    var newTags = new List<CollectionTag>();

                    // I want a union of these 2 lists. Return only elements that are in both lists, but the list types are different
                    var existingTags = series.Metadata.CollectionTags.ToList();
                    foreach (var existing in existingTags)
                    {
                        if (updateSeriesMetadataDto.Tags.SingleOrDefault(t => t.Id == existing.Id) == null)
                        {
                            // Remove tag
                            series.Metadata.CollectionTags.Remove(existing);
                        }
                    }

                    // At this point, all tags that aren't in dto have been removed.
                    foreach (var tag in updateSeriesMetadataDto.Tags)
                    {
                        var existingTag = allTags.SingleOrDefault(t => t.Title == tag.Title);
                        if (existingTag != null)
                        {
                            if (!series.Metadata.CollectionTags.Any(t => t.Title == tag.Title))
                            {
                                newTags.Add(existingTag);
                            }
                        }
                        else
                        {
                            // Add new tag
                            newTags.Add(DbFactory.CollectionTag(tag.Id, tag.Title, tag.Summary, tag.Promoted));
                        }
                    }

                    foreach (var tag in newTags)
                    {
                        series.Metadata.CollectionTags.Add(tag);
                    }
                }

                if (!_unitOfWork.HasChanges())
                {
                    return Ok("No changes to save");
                }

                if (await _unitOfWork.CommitAsync())
                {
                    foreach (var tag in updateSeriesMetadataDto.Tags)
                    {
                        await _messageHub.Clients.All.SendAsync(SignalREvents.SeriesAddedToCollection,
                            MessageFactory.SeriesAddedToCollection(tag.Id,
                                updateSeriesMetadataDto.SeriesMetadata.SeriesId));
                    }
                    return Ok("Successfully updated");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an exception when updating metadata");
                await _unitOfWork.RollbackAsync();
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


    }
}
