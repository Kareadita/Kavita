using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
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
        private readonly NumericComparer _numericComparer;

        public ReaderController(ISeriesRepository seriesRepository, IDirectoryService directoryService, ICacheService cacheService)
        {
            _seriesRepository = seriesRepository;
            _directoryService = directoryService;
            _cacheService = cacheService;
            _numericComparer = new NumericComparer();
        }

        [HttpGet("info")]
        public async Task<ActionResult<int>> GetInformation(int volumeId)
        {
            Volume volume = await _cacheService.Ensure(volumeId);
            
            if (volume == null || !volume.Files.Any())
            {
                // TODO: Move this into Ensure and return negative numbers for different error codes.
                return BadRequest("There are no files in the volume to read.");
            }

            return Ok(volume.Files.Select(x => x.NumberOfPages).Sum());

        }

        [HttpGet("image")]
        public async Task<ActionResult<ImageDto>> GetImage(int volumeId, int page)
        {
            // Temp let's iterate the directory each call to get next image
            var volume = await _cacheService.Ensure(volumeId);

            var files = _directoryService.ListFiles(_cacheService.GetCachedPagePath(volume, page));
            var array = files.ToArray();
            Array.Sort(array, _numericComparer); // TODO: Find a way to apply numericComparer to IList.
            var path = array.ElementAt(page);
            var file = await _directoryService.ReadImageAsync(path);
            file.Page = page;

            return Ok(file);
        }
    }
}