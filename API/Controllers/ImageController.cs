using System.IO;
using System.Threading.Tasks;
using API.Data;
using API.Extensions;
using API.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>
    /// Responsible for servicing up images stored in the DB
    /// </summary>
    public class ImageController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDirectoryService _directoryService;

        /// <inheritdoc />
        public ImageController(IUnitOfWork unitOfWork, IDirectoryService directoryService)
        {
            _unitOfWork = unitOfWork;
            _directoryService = directoryService;
        }

        /// <summary>
        /// Returns cover image for Chapter
        /// </summary>
        /// <param name="chapterId"></param>
        /// <returns></returns>
        [HttpGet("chapter-cover")]
        public async Task<ActionResult> GetChapterCoverImage(int chapterId)
        {
            var path = Path.Join(_directoryService.CoverImageDirectory, await _unitOfWork.ChapterRepository.GetChapterCoverImageAsync(chapterId));
            if (string.IsNullOrEmpty(path) || !_directoryService.FileSystem.File.Exists(path)) return BadRequest($"No cover image");
            var format = _directoryService.FileSystem.Path.GetExtension(path).Replace(".", "");

            Response.AddCacheHeader(path);
            return PhysicalFile(path, "image/" + format, _directoryService.FileSystem.Path.GetFileName(path));
        }

        /// <summary>
        /// Returns cover image for Volume
        /// </summary>
        /// <param name="volumeId"></param>
        /// <returns></returns>
        [HttpGet("volume-cover")]
        public async Task<ActionResult> GetVolumeCoverImage(int volumeId)
        {
            var path = Path.Join(_directoryService.CoverImageDirectory, await _unitOfWork.VolumeRepository.GetVolumeCoverImageAsync(volumeId));
            if (string.IsNullOrEmpty(path) || !_directoryService.FileSystem.File.Exists(path)) return BadRequest($"No cover image");
            var format = _directoryService.FileSystem.Path.GetExtension(path).Replace(".", "");

            Response.AddCacheHeader(path);
            return PhysicalFile(path, "image/" + format, _directoryService.FileSystem.Path.GetFileName(path));
        }

        /// <summary>
        /// Returns cover image for Series
        /// </summary>
        /// <param name="seriesId">Id of Series</param>
        /// <returns></returns>
        [HttpGet("series-cover")]
        public async Task<ActionResult> GetSeriesCoverImage(int seriesId)
        {
            var path = Path.Join(_directoryService.CoverImageDirectory, await _unitOfWork.SeriesRepository.GetSeriesCoverImageAsync(seriesId));
            if (string.IsNullOrEmpty(path) || !_directoryService.FileSystem.File.Exists(path)) return BadRequest($"No cover image");
            var format = _directoryService.FileSystem.Path.GetExtension(path).Replace(".", "");

            Response.AddCacheHeader(path);
            return PhysicalFile(path, "image/" + format, _directoryService.FileSystem.Path.GetFileName(path));
        }

        /// <summary>
        /// Returns cover image for Collection Tag
        /// </summary>
        /// <param name="collectionTagId"></param>
        /// <returns></returns>
        [HttpGet("collection-cover")]
        public async Task<ActionResult> GetCollectionCoverImage(int collectionTagId)
        {
            var path = Path.Join(_directoryService.CoverImageDirectory, await _unitOfWork.CollectionTagRepository.GetCoverImageAsync(collectionTagId));
            if (string.IsNullOrEmpty(path) || !_directoryService.FileSystem.File.Exists(path)) return BadRequest($"No cover image");
            var format = _directoryService.FileSystem.Path.GetExtension(path).Replace(".", "");

            Response.AddCacheHeader(path);
            return PhysicalFile(path, "image/" + format, _directoryService.FileSystem.Path.GetFileName(path));
        }
    }
}
