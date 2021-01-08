using System;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using API.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    public class ReaderController : BaseApiController
    {
        private readonly ISeriesRepository _seriesRepository;
        private readonly IDirectoryService _directoryService;

        public ReaderController(ISeriesRepository seriesRepository, IDirectoryService directoryService)
        {
            _seriesRepository = seriesRepository;
            _directoryService = directoryService;
        }

        [HttpGet("info")]
        public async Task<ActionResult<int>> GetInformation(int volumeId)
        {
            Volume volume = await _seriesRepository.GetVolumeAsync(volumeId);
            
            // Assume we always get first Manga File
            if (volume == null || !volume.Files.Any())
            {
                return BadRequest("There are no files in the volume to read.");
            }

            var filepath = volume.Files.ElementAt(0).FilePath;

            var extractPath = _directoryService.ExtractArchive(filepath, volumeId);
            if (string.IsNullOrEmpty(extractPath))
            {
                return BadRequest("There file is no longer there or has no images. Please rescan.");
            }

            return Ok(_directoryService.ListFiles(extractPath).Count());
        }
        
        
    }
}