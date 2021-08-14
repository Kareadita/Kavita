using System;
using System.Threading.Tasks;
using API.DTOs.Uploads;
using API.Interfaces;
using API.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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

        /// <inheritdoc />
        public UploadController(IUnitOfWork unitOfWork, IImageService imageService)
        {
            _unitOfWork = unitOfWork;
            _imageService = imageService;
        }

        /// <summary>
        /// Replaces series cover image and locks it with a base64 encoded image
        /// </summary>
        /// <param name="uploadFileDto"></param>
        /// <returns></returns>
        [Authorize(Policy = "RequireAdminRole")]
        [DisableRequestSizeLimit]
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
                    series.CoverImage = bytes;
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
        [DisableRequestSizeLimit]
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
                    tag.CoverImage = bytes;
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
                await _unitOfWork.RollbackAsync();
            }

            return BadRequest("Unable to save cover image to Series");
        }

    }
}
