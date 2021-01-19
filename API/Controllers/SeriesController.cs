using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.Extensions;
using API.Interfaces;
using AutoMapper;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers
{
    public class SeriesController : BaseApiController
    {
        private readonly ILogger<SeriesController> _logger;
        private readonly ITaskScheduler _taskScheduler;
        private readonly ICacheService _cacheService;
        private readonly IUnitOfWork _unitOfWork;

        public SeriesController(ILogger<SeriesController> logger, ITaskScheduler taskScheduler, 
            ICacheService cacheService, IUnitOfWork unitOfWork)
        {
            _logger = logger;
            _taskScheduler = taskScheduler;
            _cacheService = cacheService;
            _unitOfWork = unitOfWork;
        }
        
        [HttpGet("{seriesId}")]
        public async Task<ActionResult<SeriesDto>> GetSeries(int seriesId)
        {
            return Ok(await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId));
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
            return Ok(await _unitOfWork.SeriesRepository.GetVolumeDtoAsync(volumeId));
        }
    }
}