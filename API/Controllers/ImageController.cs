using System.Threading.Tasks;
using API.Extensions;
using API.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    public class ImageController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;

        public ImageController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [HttpGet("chapter-cover")]
        public async Task<ActionResult> GetChapterCoverImage(int chapterId)
        {
            var content = await _unitOfWork.VolumeRepository.GetChapterCoverImageAsync(chapterId);
            if (content == null) return BadRequest("No cover image");
            const string format = "jpeg";

            Response.AddCacheHeader(content);
            return File(content, "image/" + format, $"{chapterId}");
        }

        [HttpGet("volume-cover")]
        public async Task<ActionResult> GetVolumeCoverImage(int volumeId)
        {
            var content = await _unitOfWork.SeriesRepository.GetVolumeCoverImageAsync(volumeId);
            if (content == null) return BadRequest("No cover image");
            const string format = "jpeg";

            Response.AddCacheHeader(content);
            return File(content, "image/" + format, $"{volumeId}");
        }

        [HttpGet("series-cover")]
        public async Task<ActionResult> GetSeriesCoverImage(int seriesId)
        {
            var content = await _unitOfWork.SeriesRepository.GetSeriesCoverImageAsync(seriesId);
            if (content == null) return BadRequest("No cover image");
            const string format = "jpeg";

            Response.AddCacheHeader(content);
            return File(content, "image/" + format, $"{seriesId}");
        }

        [HttpGet("collection-cover")]
        public async Task<ActionResult> GetCollectionCoverImage(int collectionTagId)
        {
            var content = await _unitOfWork.CollectionTagRepository.GetCoverImageAsync(collectionTagId);
            if (content == null) return BadRequest("No cover image");
            const string format = "jpeg";

            Response.AddCacheHeader(content);
            return File(content, "image/" + format, $"{collectionTagId}");
        }
    }
}
