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
        private readonly IMapper _mapper;
        private readonly ITaskScheduler _taskScheduler;
        private readonly ISeriesRepository _seriesRepository;
        private readonly ICacheService _cacheService;
        private readonly IUserRepository _userRepository;

        public SeriesController(ILogger<SeriesController> logger, IMapper mapper, 
            ITaskScheduler taskScheduler, ISeriesRepository seriesRepository,
            ICacheService cacheService, IUserRepository userRepository)
        {
            _logger = logger;
            _mapper = mapper;
            _taskScheduler = taskScheduler;
            _seriesRepository = seriesRepository;
            _cacheService = cacheService;
            _userRepository = userRepository;
        }
        
        [HttpGet("{seriesId}")]
        public async Task<ActionResult<SeriesDto>> GetSeries(int seriesId)
        {
            return Ok(await _seriesRepository.GetSeriesDtoByIdAsync(seriesId));
        }

        [Authorize(Policy = "RequireAdminRole")]
        [HttpDelete("{seriesId}")]
        public async Task<ActionResult<bool>> DeleteSeries(int seriesId)
        {
            var username = User.GetUsername();
            var volumes = (await _seriesRepository.GetVolumesForSeriesAsync(new []{seriesId})).Select(x => x.Id).ToArray();
            _logger.LogInformation($"Series {seriesId} is being deleted by {username}.");
            var result = await _seriesRepository.DeleteSeriesAsync(seriesId);

            if (result)
            {
                BackgroundJob.Enqueue(() => _cacheService.CleanupVolumes(volumes));
            }
            return Ok(result);
        }

        [HttpGet("volumes")]
        public async Task<ActionResult<IEnumerable<VolumeDto>>> GetVolumes(int seriesId, bool forUser = true)
        {
            if (forUser)
            {
                var user = await _userRepository.GetUserByUsernameAsync(User.GetUsername());
                return Ok(await _seriesRepository.GetVolumesDtoAsync(seriesId, user.Id));
            }
            return Ok(await _seriesRepository.GetVolumesDtoAsync(seriesId)); // TODO: Refactor out forUser = false since everything is user based
        }
        
        [HttpGet("volume")]
        public async Task<ActionResult<VolumeDto>> GetVolume(int volumeId)
        {
            return Ok(await _seriesRepository.GetVolumeDtoAsync(volumeId));
        }
    }
}