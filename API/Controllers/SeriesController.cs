using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
using API.Extensions;
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
            
            // TODO: check if new name isn't an existing series
            var existingSeries = await _unitOfWork.SeriesRepository.GetSeriesByNameAsync(updateSeries.Name); // NOTE: This isnt checking library
            if (existingSeries != null && existingSeries.Id != series.Id)
            {
                return BadRequest("A series already exists with this name. Name must be unique.");
            }
            series.Name = updateSeries.Name;
            series.LocalizedName = updateSeries.LocalizedName;
            series.SortName = updateSeries.SortName;
            series.Summary = updateSeries.Summary;
            //series.CoverImage = updateSeries.CoverImage;
            
            _unitOfWork.SeriesRepository.Update(series);

            if (await _unitOfWork.Complete())
            {
                return Ok();
            }
            
            return BadRequest("There was an error with updating the series");
        }

        [HttpGet("recently-added")]
        public async Task<ActionResult<IEnumerable<SeriesDto>>> GetRecentlyAdded(int libraryId = 0)
        {
            return Ok(await _unitOfWork.SeriesRepository.GetRecentlyAdded(libraryId));
        }
    }
}