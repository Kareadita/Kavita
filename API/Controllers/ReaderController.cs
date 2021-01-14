using System.Threading.Tasks;
using API.DTOs;
using API.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    public class ReaderController : BaseApiController
    {
        private readonly IDirectoryService _directoryService;
        private readonly ICacheService _cacheService;

        public ReaderController(IDirectoryService directoryService, ICacheService cacheService)
        {
            _directoryService = directoryService;
            _cacheService = cacheService;
        }

        [HttpGet("image")]
        public async Task<ActionResult<ImageDto>> GetImage(int volumeId, int page)
        {
            // Temp let's iterate the directory each call to get next image
            var volume = await _cacheService.Ensure(volumeId);

            var path = _cacheService.GetCachedPagePath(volume, page);
            var file = await _directoryService.ReadImageAsync(path);
            file.Page = page;

            return Ok(file);
        }
    }
}