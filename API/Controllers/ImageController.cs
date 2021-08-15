﻿using System.Threading.Tasks;
using API.Extensions;
using API.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>
    /// Responsible for servicing up images stored in the DB
    /// </summary>
    public class ImageController : BaseApiController
    {
        private const string Format = "jpeg";
        private readonly IUnitOfWork _unitOfWork;

        /// <inheritdoc />
        public ImageController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// Returns cover image for Chapter
        /// </summary>
        /// <param name="chapterId"></param>
        /// <returns></returns>
        [HttpGet("chapter-cover")]
        public async Task<ActionResult> GetChapterCoverImage(int chapterId)
        {
            var content = await _unitOfWork.VolumeRepository.GetChapterCoverImageAsync(chapterId);
            if (content == null) return BadRequest("No cover image");

            Response.AddCacheHeader(content);
            return File(content, "image/" + Format, $"{chapterId}");
        }

        /// <summary>
        /// Returns cover image for Volume
        /// </summary>
        /// <param name="volumeId"></param>
        /// <returns></returns>
        [HttpGet("volume-cover")]
        public async Task<ActionResult> GetVolumeCoverImage(int volumeId)
        {
            var content = await _unitOfWork.SeriesRepository.GetVolumeCoverImageAsync(volumeId);
            if (content == null) return BadRequest("No cover image");

            Response.AddCacheHeader(content);
            return File(content, "image/" + Format, $"{volumeId}");
        }

        /// <summary>
        /// Returns cover image for Series
        /// </summary>
        /// <param name="seriesId">Id of Series</param>
        /// <returns></returns>
        [HttpGet("series-cover")]
        public async Task<ActionResult> GetSeriesCoverImage(int seriesId)
        {
            var content = await _unitOfWork.SeriesRepository.GetSeriesCoverImageAsync(seriesId);
            if (content == null) return BadRequest("No cover image");

            Response.AddCacheHeader(content);
            return File(content, "image/" + Format, $"{seriesId}");
        }

        /// <summary>
        /// Returns cover image for Collection Tag
        /// </summary>
        /// <param name="collectionTagId"></param>
        /// <returns></returns>
        [HttpGet("collection-cover")]
        public async Task<ActionResult> GetCollectionCoverImage(int collectionTagId)
        {
            var content = await _unitOfWork.CollectionTagRepository.GetCoverImageAsync(collectionTagId);
            if (content == null) return BadRequest("No cover image");

            Response.AddCacheHeader(content);
            return File(content, "image/" + Format, $"{collectionTagId}");
        }
    }
}
