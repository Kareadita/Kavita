using System.Collections.Generic;
using System.Linq;
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
            var volumes = (await _unitOfWork.SeriesRepository.GetVolumesForSeriesAsync(new []{seriesId})).Select(x => x.Id).ToArray();
            _logger.LogInformation($"Series {seriesId} is being deleted by {username}.");
            var result = await _unitOfWork.SeriesRepository.DeleteSeriesAsync(seriesId);

            if (result)
            {
                _taskScheduler.CleanupVolumes(volumes);
            }
            return Ok(result);
        }

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

        [HttpPost("update-rating")]
        public async Task<ActionResult> UpdateSeriesRating(UpdateSeriesRatingDto updateSeriesRatingDto)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            var userRating = await _unitOfWork.UserRepository.GetUserRating(updateSeriesRatingDto.SeriesId, user.Id) ??
                             new AppUserRating();

            userRating.Rating = updateSeriesRatingDto.UserRating;
            userRating.Review = updateSeriesRatingDto.UserReview;
            userRating.SeriesId = updateSeriesRatingDto.SeriesId;

            _unitOfWork.UserRepository.AddRatingTracking(userRating);
            user.Ratings ??= new List<AppUserRating>();
            user.Ratings.Add(userRating);


            if (!await _unitOfWork.Complete()) return BadRequest("There was a critical error.");

            return Ok();
        }
    }
}