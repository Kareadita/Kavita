using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
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
            // TODO: Write custom methods to just get the byte[] as fast as possible
            var chapter = await _unitOfWork.VolumeRepository.GetChapterAsync(chapterId);
            var content = chapter.CoverImage;
            var format = "jpeg"; //Path.GetExtension("jpeg").Replace(".", "");
            
            // Calculates SHA1 Hash for byte[]
            using var sha1 = new System.Security.Cryptography.SHA1CryptoServiceProvider();
            Response.Headers.Add("ETag", string.Concat(sha1.ComputeHash(content).Select(x => x.ToString("X2"))));
            Response.Headers.Add("Cache-Control", "private");

            return File(content, "image/" + format);
        }

        [HttpGet("volume-cover")]
        public async Task<ActionResult> GetVolumeCoverImage(int volumeId)
        {
            var volume = await _unitOfWork.SeriesRepository.GetVolumeAsync(volumeId);
            var content = volume.CoverImage;
            var format = "jpeg"; //Path.GetExtension("jpeg").Replace(".", "");
            
            // Calculates SHA1 Hash for byte[]
            using var sha1 = new System.Security.Cryptography.SHA1CryptoServiceProvider();
            Response.Headers.Add("ETag", string.Concat(sha1.ComputeHash(content).Select(x => x.ToString("X2"))));
            Response.Headers.Add("Cache-Control", "private");

            return File(content, "image/" + format);
        }
        
        [HttpGet("series-cover")]
        public async Task<ActionResult> GetSeriesCoverImage(int seriesId)
        {
            var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId);
            var content = series.CoverImage;
            var format = "jpeg"; //Path.GetExtension("jpeg").Replace(".", "");

            if (content.Length == 0)
            {
                // How do I handle? 
            }
            
            // Calculates SHA1 Hash for byte[]
            using var sha1 = new System.Security.Cryptography.SHA1CryptoServiceProvider();
            Response.Headers.Add("ETag", string.Concat(sha1.ComputeHash(content).Select(x => x.ToString("X2"))));
            Response.Headers.Add("Cache-Control", "private");

            return File(content, "image/" + format);
        }
    }
}