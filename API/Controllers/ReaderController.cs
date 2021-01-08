using System;
using System.IO;
using System.IO.Compression;
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

            // NOTE: I'm starting to think this should actually cache the information about Volume/Manga file in the DB. 
            // It will be updated each time this is called which is on open of a manga.
            return Ok(_directoryService.ListFiles(extractPath).Count());
        }

        [HttpGet("image")]
        public async Task<ActionResult<ImageDto>> GetImage(int volumeId, int page)
        {
            // Temp let's iterate the directory each call to get next image
            var files = _directoryService.ListFiles(_directoryService.GetExtractPath(volumeId));
            var path = files.ElementAt(page);
            var file = await _directoryService.ReadImageAsync(path);
            file.Page = page;

            return Ok(file);

            //return File(await _directoryService.ReadImageAsync(path), "image/jpg", filename);
        }
    }
}