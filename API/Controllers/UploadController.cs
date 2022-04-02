﻿using System;
using System.IO;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Uploads;
using API.Extensions;
using API.Services;
using Flurl.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NetVips;

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
        private readonly IDirectoryService _directoryService;

        /// <inheritdoc />
        public UploadController(IUnitOfWork unitOfWork, IImageService imageService, ILogger<UploadController> logger,
            ITaskScheduler taskScheduler, IDirectoryService directoryService)
        {
            _unitOfWork = unitOfWork;
            _imageService = imageService;
            _logger = logger;
            _taskScheduler = taskScheduler;
            _directoryService = directoryService;
        }

        /// <summary>
        /// This stores a file (image) in temp directory for use in a cover image replacement flow.
        /// This is automatically cleaned up.
        /// </summary>
        /// <param name="dto">Escaped url to download from</param>
        /// <returns>filename</returns>
        [Authorize(Policy = "RequireAdminRole")]
        [HttpPost("upload-by-url")]
        public async Task<ActionResult<string>> GetImageFromFile(UploadUrlDto dto)
        {
            var dateString = $"{DateTime.Now.ToShortDateString()}_{DateTime.Now.ToLongTimeString()}".Replace("/", "_").Replace(":", "_");
            var format = _directoryService.FileSystem.Path.GetExtension(dto.Url.Split('?')[0]).Replace(".", "");
            try
            {
                var path = await dto.Url
                    .DownloadFileAsync(_directoryService.TempDirectory, $"coverupload_{dateString}.{format}");

                if (string.IsNullOrEmpty(path) || !_directoryService.FileSystem.File.Exists(path))
                    return BadRequest($"Could not download file");

                return $"coverupload_{dateString}.{format}";
            }
            catch (FlurlHttpException ex)
            {
                // Unauthorized
                if (ex.StatusCode == 401)
                    return BadRequest("The server requires authentication to load the url externally");
            }

            return BadRequest("Unable to download image, please use another url or upload by file");
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
                var filePath = _imageService.CreateThumbnailFromBase64(uploadFileDto.Url, ImageService.GetSeriesFormat(uploadFileDto.Id));
                var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(uploadFileDto.Id);

                if (!string.IsNullOrEmpty(filePath))
                {
                    series.CoverImage = filePath;
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
                var filePath = _imageService.CreateThumbnailFromBase64(uploadFileDto.Url, $"{ImageService.GetCollectionTagFormat(uploadFileDto.Id)}");
                var tag = await _unitOfWork.CollectionTagRepository.GetTagAsync(uploadFileDto.Id);

                if (!string.IsNullOrEmpty(filePath))
                {
                    tag.CoverImage = filePath;
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
                var chapter = await _unitOfWork.ChapterRepository.GetChapterAsync(uploadFileDto.Id);
                var filePath = _imageService.CreateThumbnailFromBase64(uploadFileDto.Url, $"{ImageService.GetChapterFormat(uploadFileDto.Id, chapter.VolumeId)}");

                if (!string.IsNullOrEmpty(filePath))
                {
                    chapter.CoverImage = filePath;
                    chapter.CoverImageLocked = true;
                    _unitOfWork.ChapterRepository.Update(chapter);
                    var volume = await _unitOfWork.VolumeRepository.GetVolumeAsync(chapter.VolumeId);
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
                var originalFile = chapter.CoverImage;
                chapter.CoverImage = string.Empty;
                chapter.CoverImageLocked = false;
                _unitOfWork.ChapterRepository.Update(chapter);
                var volume = await _unitOfWork.VolumeRepository.GetVolumeAsync(chapter.VolumeId);
                volume.CoverImage = chapter.CoverImage;
                _unitOfWork.VolumeRepository.Update(volume);
                var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(volume.SeriesId);

                if (_unitOfWork.HasChanges())
                {
                    await _unitOfWork.CommitAsync();
                    System.IO.File.Delete(originalFile);
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
