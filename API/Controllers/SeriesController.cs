using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs;
using API.Interfaces;
using AutoMapper;
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

        public SeriesController(ILogger<SeriesController> logger, IMapper mapper, 
            ITaskScheduler taskScheduler, ISeriesRepository seriesRepository)
        {
            _logger = logger;
            _mapper = mapper;
            _taskScheduler = taskScheduler;
            _seriesRepository = seriesRepository;
        }
        
        [HttpGet("{seriesId}")]
        public async Task<ActionResult<SeriesDto>> GetSeries(int seriesId)
        {
            return Ok(await _seriesRepository.GetSeriesDtoByIdAsync(seriesId));
        }

        [HttpGet("volumes")]
        public async Task<ActionResult<IEnumerable<VolumeDto>>> GetVolumes(int seriesId)
        {
            return Ok(await _seriesRepository.GetVolumesDtoAsync(seriesId));
        }
        
        [HttpGet("volume")]
        public async Task<ActionResult<VolumeDto>> GetVolume(int volumeId)
        {
            return Ok(await _seriesRepository.GetVolumeDtoAsync(volumeId));
        }
    }
}