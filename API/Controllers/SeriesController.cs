using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Helpers;
using API.Interfaces;
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

        public SeriesController(ILogger<SeriesController> logger, ITaskScheduler taskScheduler, IUnitOfWork unitOfWork)
        {
            _logger = logger;
            _taskScheduler = taskScheduler;
            _unitOfWork = unitOfWork;
        }
        
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Series>>> GetSeriesForLibrary(int libraryId, [FromQuery] UserParams userParams)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            var series =
                await _unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdAsync(libraryId, user.Id, userParams);
            
            // Apply progress/rating information (I can't work out how to do this in initial query)
            if (series == null) return BadRequest("Could not get series for library");

            await _unitOfWork.SeriesRepository.AddSeriesModifiers(user.Id, series);
            
            Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);
            
            return Ok(series);
        }
        
        [HttpGet("{seriesId}")]
        public async Task<ActionResult<SeriesDto>> GetSeries(int seriesId)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            return Ok(await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, user.Id));
        }

        [Authorize(Policy = "RequireAdminRole")]
        [HttpDelete("{seriesId}")]
        public async Task<ActionResult<bool>> DeleteSeries(int seriesId)
        {
            var username = User.GetUsername();
            var chapterIds = (await _unitOfWork.SeriesRepository.GetChapterIdsForSeriesAsync(new []{seriesId}));
            _logger.LogInformation("Series {SeriesId} is being deleted by {UserName}", seriesId, username);
            var result = await _unitOfWork.SeriesRepository.DeleteSeriesAsync(seriesId);

            if (result)
            {
                _taskScheduler.CleanupChapters(chapterIds);
            }
            return Ok(result);
        }

        /// <summary>
        /// Returns All volumes for a series with progress information and Chapters
        /// </summary>
        /// <param name="seriesId"></param>
        /// <returns></returns>
        [HttpGet("volumes")]
        public async Task<ActionResult<IEnumerable<VolumeDto>>> GetVolumes(int seriesId)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            return Ok(await _unitOfWork.SeriesRepository.GetVolumesDtoAsync(seriesId, user.Id));
        }
        
        [HttpGet("volume")]
        public async Task<ActionResult<VolumeDto>> GetVolume(int volumeId)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            return Ok(await _unitOfWork.SeriesRepository.GetVolumeDtoAsync(volumeId, user.Id));
        }
        
        [HttpGet("chapter")]
        public async Task<ActionResult<VolumeDto>> GetChapter(int chapterId)
        {
            return Ok(await _unitOfWork.VolumeRepository.GetChapterDtoAsync(chapterId));
        }

        
        

        [HttpPost("update-rating")]
        public async Task<ActionResult> UpdateSeriesRating(UpdateSeriesRatingDto updateSeriesRatingDto)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
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

            if (!await _unitOfWork.Complete()) return BadRequest("There was a critical error.");

            return Ok();
        }

        [HttpPost]
        public async Task<ActionResult> UpdateSeries(UpdateSeriesDto updateSeries)
        {
            _logger.LogInformation("{UserName} is updating Series {SeriesName}", User.GetUsername(), updateSeries.Name);

            var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(updateSeries.Id);

            if (series == null) return BadRequest("Series does not exist");
            
            if (series.Name != updateSeries.Name && await _unitOfWork.SeriesRepository.DoesSeriesNameExistInLibrary(updateSeries.Name))
            {
                return BadRequest("A series already exists in this library with this name. Series Names must be unique to a library.");
            }
            series.Name = updateSeries.Name;
            series.LocalizedName = updateSeries.LocalizedName;
            series.SortName = updateSeries.SortName;
            series.Summary = updateSeries.Summary;

            _unitOfWork.SeriesRepository.Update(series);

            if (await _unitOfWork.Complete())
            {
                return Ok();
            }
            
            return BadRequest("There was an error with updating the series");
        }

        [HttpGet("recently-added")]
        public async Task<ActionResult<IEnumerable<SeriesDto>>> GetRecentlyAdded(int libraryId = 0, int limit = 20)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            if (user == null) return Ok(Array.Empty<SeriesDto>());
            return Ok(await _unitOfWork.SeriesRepository.GetRecentlyAdded(user.Id, libraryId, limit));
        }
        
        [HttpGet("in-progress")]
        public async Task<ActionResult<IEnumerable<SeriesDto>>> GetInProgress(int libraryId = 0, int limit = 20)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            if (user == null) return Ok(Array.Empty<SeriesDto>());
            return Ok(await _unitOfWork.SeriesRepository.GetInProgress(user.Id, libraryId, limit));
        }

        [Authorize(Policy = "RequireAdminRole")]
        [HttpPost("refresh-metadata")]
        public ActionResult RefreshSeriesMetadata(RefreshSeriesDto refreshSeriesDto)
        {
            _taskScheduler.RefreshSeriesMetadata(refreshSeriesDto.LibraryId, refreshSeriesDto.SeriesId);
            return Ok();
        }

        [HttpGet("metadata")]
        public async Task<ActionResult<SeriesMetadataDto>> GetSeriesMetadata(int seriesId)
        {
            var metadata = await _unitOfWork.SeriesRepository.GetSeriesMetadata(seriesId);
            if (metadata == null)
            {
                // Create one
                var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId);
                
                series.Metadata = new SeriesMetadata();
                if (await _unitOfWork.Complete())
                {
                    return await _unitOfWork.SeriesRepository.GetSeriesMetadata(seriesId);
                }
                _logger.LogCritical("Could not create a series metadata for {SeriesId}", seriesId);
                return BadRequest("Could not create a series metadata");
            }
            return Ok(metadata);
        }
        
        [HttpPost("metadata")]
        public async Task<ActionResult> UpdateSeriesMetadata(UpdateSeriesMetadataDto updateSeriesMetadataDto)
        {
            var seriesId = updateSeriesMetadataDto.SeriesMetadata.SeriesId;
            var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId);
            if (series.Metadata == null)
            {
                series.Metadata = new SeriesMetadata()
                {
                    CollectionTags = updateSeriesMetadataDto.Tags.Select(dto => new CollectionTag()
                    {
                        Id = dto.Id,
                        NormalizedTitle = Parser.Parser.Normalize(dto.Title).ToUpper(),
                        Title = dto.Title,
                        Promoted = dto.Promoted
                    }).ToList()
                };
            }
            else
            {
                // TODO: We need to manually patch existing items that already exist
                var newTags = new List<CollectionTag>();
                foreach (var tag in updateSeriesMetadataDto.Tags)
                {
                    // TODO: Ensure we can delete tags
                    series.Metadata.CollectionTags ??= new List<CollectionTag>();
                    var existingTag = series.Metadata.CollectionTags.SingleOrDefault(t => t.Title == tag.Title);
                    if (existingTag != null)
                    {
                        // Update existingTag    
                        existingTag.Promoted = tag.Promoted;
                        existingTag.Title = tag.Title;
                        existingTag.NormalizedTitle = Parser.Parser.Normalize(tag.Title).ToUpper();
                    }
                    else
                    {
                        // Add new tag
                        newTags.Add(new CollectionTag()
                        {
                            Id = tag.Id,
                            NormalizedTitle = Parser.Parser.Normalize(tag.Title).ToUpper(),
                            Title = tag.Title,
                            Promoted = tag.Promoted
                        });
                    }
                }

                foreach (var tag in newTags)
                {
                    series.Metadata.CollectionTags.Add(tag);
                }
            }
            
            if (await _unitOfWork.Complete())
            {
                return Ok();
            }

            return BadRequest("Could not update metadata");
        }
    }
}