using System;
using System.Threading.Tasks;
using API.DTOs.Uploads;
using API.Interfaces;
using API.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers
{
    /// <summary>
    ///
    /// </summary>
    [Authorize(Policy = "RequireAdminRole")]
    public class UploadController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IImageService _imageService;
        private readonly ILogger<UploadController> _logger;
        private readonly ITaskScheduler _taskScheduler;

        /// <inheritdoc />
        public UploadController(IUnitOfWork unitOfWork, IImageService imageService, ILogger<UploadController> logger, ITaskScheduler taskScheduler)
        {
            _unitOfWork = unitOfWork;
            _imageService = imageService;
            _logger = logger;
            _taskScheduler = taskScheduler;
        }

        /// <summary>
        /// Replaces series cover image and locks it with a base64 encoded image
        /// </summary>
        /// <param name="uploadFileDto"></param>
        /// <returns></returns>
        [Authorize(Policy = "RequireAdminRole")]
        [RequestSizeLimit(8_000_000)]
        [HttpPost("series")]
        public async Task<ActionResult> UploadSeriesCoverImageFromUrl(UploadFileDto uploadFileDto)
        {
            // Check if Url is non empty, request the image and place in temp, then ask image service to handle it.
            // See if we can do this all in memory without touching underlying system
            if (string.IsNullOrEmpty(uploadFileDto.Url))
            {
                return BadRequest("You must pass a url to use");
            }

            try
            {
                var bytes = _imageService.CreateThumbnailFromBase64(uploadFileDto.Url);
                var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(uploadFileDto.Id);

                if (bytes.Length > 0)
                {
                    series.CoverImage = String.Empty; // TODO: Correct this (bytes)
                    series.CoverImageLocked = true;
                    _unitOfWork.SeriesRepository.Update(series);
                }

                if (_unitOfWork.HasChanges())
                {
                    await _unitOfWork.CommitAsync();
                    return Ok();
                }

            }
            catch (Exception e)
            {
                _logger.LogError(e, "There was an issue uploading cover image for Series {Id}", uploadFileDto.Id);
                await _unitOfWork.RollbackAsync();
            }

            return BadRequest("Unable to save cover image to Series");
        }

        /// <summary>
        /// Replaces collection tag cover image and locks it with a base64 encoded image
        /// </summary>
        /// <param name="uploadFileDto"></param>
        /// <returns></returns>
        [Authorize(Policy = "RequireAdminRole")]
        [RequestSizeLimit(8_000_000)]
        [HttpPost("collection")]
        public async Task<ActionResult> UploadCollectionCoverImageFromUrl(UploadFileDto uploadFileDto)
        {
            // Check if Url is non empty, request the image and place in temp, then ask image service to handle it.
            // See if we can do this all in memory without touching underlying system
            if (string.IsNullOrEmpty(uploadFileDto.Url))
            {
                return BadRequest("You must pass a url to use");
            }

            try
            {
                var bytes = _imageService.CreateThumbnailFromBase64(uploadFileDto.Url);
                var tag = await _unitOfWork.CollectionTagRepository.GetTagAsync(uploadFileDto.Id);

                if (bytes.Length > 0)
                {
                    tag.CoverImage = String.Empty;
                    ; //TODO: Fix bytes;
                    tag.CoverImageLocked = true;
                    _unitOfWork.CollectionTagRepository.Update(tag);
                }

                if (_unitOfWork.HasChanges())
                {
                    await _unitOfWork.CommitAsync();
                    return Ok();
                }

            }
            catch (Exception e)
            {
                _logger.LogError(e, "There was an issue uploading cover image for Collection Tag {Id}", uploadFileDto.Id);
                await _unitOfWork.RollbackAsync();
            }

            return BadRequest("Unable to save cover image to Collection Tag");
        }

        /// <summary>
        /// Replaces chapter cover image and locks it with a base64 encoded image. This will update the parent volume's cover image.
        /// </summary>
        /// <param name="uploadFileDto"></param>
        /// <returns></returns>
        [Authorize(Policy = "RequireAdminRole")]
        [RequestSizeLimit(8_000_000)]
        [HttpPost("chapter")]
        public async Task<ActionResult> UploadChapterCoverImageFromUrl(UploadFileDto uploadFileDto)
        {
            // Check if Url is non empty, request the image and place in temp, then ask image service to handle it.
            // See if we can do this all in memory without touching underlying system
            if (string.IsNullOrEmpty(uploadFileDto.Url))
            {
                return BadRequest("You must pass a url to use");
            }

            try
            {
                var bytes = _imageService.CreateThumbnailFromBase64(uploadFileDto.Url);

                if (bytes.Length > 0)
                {
                    var chapter = await _unitOfWork.ChapterRepository.GetChapterAsync(uploadFileDto.Id);
                    chapter.CoverImage = string.Empty;// TODO: bytes;
                    chapter.CoverImageLocked = true;
                    _unitOfWork.ChapterRepository.Update(chapter);
                    var volume = await _unitOfWork.SeriesRepository.GetVolumeAsync(chapter.VolumeId);
                    volume.CoverImage = chapter.CoverImage;
                    _unitOfWork.VolumeRepository.Update(volume);
                }

                if (_unitOfWork.HasChanges())
                {
                    await _unitOfWork.CommitAsync();
                    return Ok();
                }

            }
            catch (Exception e)
            {
                _logger.LogError(e, "There was an issue uploading cover image for Chapter {Id}", uploadFileDto.Id);
                await _unitOfWork.RollbackAsync();
            }

            return BadRequest("Unable to save cover image to Chapter");
        }

        /// <summary>
        /// Replaces chapter cover image and locks it with a base64 encoded image. This will update the parent volume's cover image.
        /// </summary>
        /// <param name="uploadFileDto">Does not use Url property</param>
        /// <returns></returns>
        [Authorize(Policy = "RequireAdminRole")]
        [HttpPost("reset-chapter-lock")]
        public async Task<ActionResult> ResetChapterLock(UploadFileDto uploadFileDto)
        {
            try
            {
                var chapter = await _unitOfWork.ChapterRepository.GetChapterAsync(uploadFileDto.Id);
                chapter.CoverImage = string.Empty; //Array.Empty<byte>();
                chapter.CoverImageLocked = false;
                _unitOfWork.ChapterRepository.Update(chapter);
                var volume = await _unitOfWork.SeriesRepository.GetVolumeAsync(chapter.VolumeId);
                volume.CoverImage = chapter.CoverImage;
                _unitOfWork.VolumeRepository.Update(volume);
                var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(volume.SeriesId);

                if (_unitOfWork.HasChanges())
                {
                    await _unitOfWork.CommitAsync();
                    _taskScheduler.RefreshSeriesMetadata(series.LibraryId, series.Id, true);
                    return Ok();
                }

            }
            catch (Exception e)
            {
                _logger.LogError(e, "There was an issue resetting cover lock for Chapter {Id}", uploadFileDto.Id);
                await _unitOfWork.RollbackAsync();
            }

            return BadRequest("Unable to resetting cover lock for Chapter");
        }

    }
}
