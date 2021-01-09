using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
using API.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    public class ReaderController : BaseApiController
    {
        private readonly ISeriesRepository _seriesRepository;
        private readonly IDirectoryService _directoryService;
        private readonly ICacheService _cacheService;

        public ReaderController(ISeriesRepository seriesRepository, IDirectoryService directoryService, ICacheService cacheService)
        {
            _seriesRepository = seriesRepository;
            _directoryService = directoryService;
            _cacheService = cacheService;
        }

        [HttpGet("info")]
        public async Task<ActionResult<int>> GetInformation(int volumeId)
        {
            // TODO: This will be refactored out. No longer needed.
            Volume volume = await _seriesRepository.GetVolumeAsync(volumeId);
            
            // Assume we always get first Manga File
            if (volume == null || !volume.Files.Any())
            {
                return BadRequest("There are no files in the volume to read.");
            }
            
            _cacheService.Ensure(volumeId);

            return Ok(volume.Files.Select(x => x.NumberOfPages).Sum());

        }

        [HttpGet("image")]
        public async Task<ActionResult<ImageDto>> GetImage(int volumeId, int page)
        {
            // Temp let's iterate the directory each call to get next image
            _cacheService.Ensure(volumeId);
            
            
            
            var files = _directoryService.ListFiles(_directoryService.GetExtractPath(volumeId));
            var path = files.ElementAt(page);
            var file = await _directoryService.ReadImageAsync(path);
            file.Page = page;

            return Ok(file);
        }
    }
}