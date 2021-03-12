using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.Extensions;
using API.Interfaces;
using API.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers
{
    public class ImageController : BaseApiController
    {
        private readonly IDirectoryService _directoryService;
        private readonly ICacheService _cacheService;
        private readonly ILogger<ImageController> _logger;
        private readonly IUnitOfWork _unitOfWork;

        public ImageController(IDirectoryService directoryService, ICacheService cacheService,
            ILogger<ImageController> logger, IUnitOfWork unitOfWork)
        {
            _directoryService = directoryService;
            _cacheService = cacheService;
            _logger = logger;
            _unitOfWork = unitOfWork;
        }
        
        [HttpGet("chapter-cover")]
        public async Task<ActionResult> GetChapterCoverImage(int chapterId)
        {
            var content = await _unitOfWork.VolumeRepository.GetChapterCoverImageAsync(chapterId);
            if (content == null) return BadRequest("No cover image");
            const string format = "jpeg";

            Response.AddCacheHeader(content);
            return File(content, "image/" + format);
        }

        [HttpGet("volume-cover")]
        public async Task<ActionResult> GetVolumeCoverImage(int volumeId)
        {
            var content = await _unitOfWork.SeriesRepository.GetVolumeCoverImageAsync(volumeId);
            if (content == null) return BadRequest("No cover image");
            const string format = "jpeg";

            Response.AddCacheHeader(content);
            return File(content, "image/" + format);
        }
        
        [HttpGet("series-cover")]
        public async Task<ActionResult> GetSeriesCoverImage(int seriesId)
        {
            var content = await _unitOfWork.SeriesRepository.GetSeriesCoverImageAsync(seriesId);
            if (content == null) return BadRequest("No cover image");
            const string format = "jpeg";

            Response.AddCacheHeader(content);
            return File(content, "image/" + format);
        }
    }
}