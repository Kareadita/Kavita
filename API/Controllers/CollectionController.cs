﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.DTOs.CollectionTags;
using API.Entities;
using API.Extensions;
using API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>
    /// APIs for Collections
    /// </summary>
    public class CollectionController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;

        /// <inheritdoc />
        public CollectionController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// Return a list of all collection tags on the server
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<IEnumerable<CollectionTagDto>> GetAllTags()
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            var isAdmin = await _unitOfWork.UserRepository.IsUserAdmin(user);
            if (isAdmin)
            {
                return await _unitOfWork.CollectionTagRepository.GetAllTagDtosAsync();
            }
            return await _unitOfWork.CollectionTagRepository.GetAllPromotedTagDtosAsync();
        }

        /// <summary>
        /// Searches against the collection tags on the DB and returns matches that meet the search criteria.
        /// <remarks>Search strings will be cleaned of certain fields, like %</remarks>
        /// </summary>
        /// <param name="queryString">Search term</param>
        /// <returns></returns>
        [Authorize(Policy = "RequireAdminRole")]
        [HttpGet("search")]
        public async Task<IEnumerable<CollectionTagDto>> SearchTags(string queryString)
        {
            queryString ??= "";
            queryString = queryString.Replace(@"%", "");
            if (queryString.Length == 0) return await _unitOfWork.CollectionTagRepository.GetAllTagDtosAsync();

            return await _unitOfWork.CollectionTagRepository.SearchTagDtosAsync(queryString);
        }

        /// <summary>
        /// Updates an existing tag with a new title, promotion status, and summary.
        /// <remarks>UI does not contain controls to update title</remarks>
        /// </summary>
        /// <param name="updatedTag"></param>
        /// <returns></returns>
        [Authorize(Policy = "RequireAdminRole")]
        [HttpPost("update")]
        public async Task<ActionResult> UpdateTagPromotion(CollectionTagDto updatedTag)
        {
            var existingTag = await _unitOfWork.CollectionTagRepository.GetTagAsync(updatedTag.Id);
            if (existingTag == null) return BadRequest("This tag does not exist");

            existingTag.Promoted = updatedTag.Promoted;
            existingTag.Title = updatedTag.Title.Trim();
            existingTag.NormalizedTitle = Parser.Parser.Normalize(updatedTag.Title).ToUpper();
            existingTag.Summary = updatedTag.Summary.Trim();

            if (_unitOfWork.HasChanges())
            {
                if (await _unitOfWork.CommitAsync())
                {
                    return Ok("Tag updated successfully");
                }
            }
            else
            {
                return Ok("Tag updated successfully");
            }

            return BadRequest("Something went wrong, please try again");
        }


        [HttpPost("update-for-series")]
        public async Task<ActionResult> AddToMultipleSeries(CollectionTagBulkAddDto dto)
        {
            var tag = await _unitOfWork.CollectionTagRepository.GetFullTagAsync(dto.CollectionTagId);
            if (tag == null) return BadRequest("Not a valid Tag Id");

            var seriesMetadatas = await _unitOfWork.SeriesRepository.GetSeriesMetadataForIdsAsync(dto.SeriesIds);
            foreach (var metadata in seriesMetadatas)
            {
                if (!metadata.CollectionTags.Any(t => t.Title == tag.Title))
                {
                    tag.SeriesMetadatas.Add(metadata);
                    _unitOfWork.CollectionTagRepository.Update(tag);
                }
            }

            if (!_unitOfWork.HasChanges()) return Ok();
            if (await _unitOfWork.CommitAsync())
            {
                return Ok();
            }
            return BadRequest("There was an issue updating series with collection tag");
        }

        /// <summary>
        /// For a given tag, update the summary if summary has changed and remove a set of series from the tag.
        /// </summary>
        /// <param name="updateSeriesForTagDto"></param>
        /// <returns></returns>
        [Authorize(Policy = "RequireAdminRole")]
        [HttpPost("update-series")]
        public async Task<ActionResult> UpdateSeriesForTag(UpdateSeriesForTagDto updateSeriesForTagDto)
        {
            try
            {
                var tag = await _unitOfWork.CollectionTagRepository.GetFullTagAsync(updateSeriesForTagDto.Tag.Id);
                if (tag == null) return BadRequest("Not a valid Tag");
                tag.SeriesMetadatas ??= new List<SeriesMetadata>();

                // Check if Tag has updated (Summary)
                if (tag.Summary == null || !tag.Summary.Equals(updateSeriesForTagDto.Tag.Summary))
                {
                    tag.Summary = updateSeriesForTagDto.Tag.Summary;
                    _unitOfWork.CollectionTagRepository.Update(tag);
                }

                tag.CoverImageLocked = updateSeriesForTagDto.Tag.CoverImageLocked;

                if (!updateSeriesForTagDto.Tag.CoverImageLocked)
                {
                    tag.CoverImageLocked = false;
                    tag.CoverImage = string.Empty;
                    _unitOfWork.CollectionTagRepository.Update(tag);
                }

                foreach (var seriesIdToRemove in updateSeriesForTagDto.SeriesIdsToRemove)
                {
                    tag.SeriesMetadatas.Remove(tag.SeriesMetadatas.Single(sm => sm.SeriesId == seriesIdToRemove));
                }


                if (tag.SeriesMetadatas.Count == 0)
                {
                    _unitOfWork.CollectionTagRepository.Remove(tag);
                }

                if (!_unitOfWork.HasChanges()) return Ok("No updates");

                if (await _unitOfWork.CommitAsync())
                {
                    return Ok("Tag updated");
                }
            }
            catch (Exception)
            {
                await _unitOfWork.RollbackAsync();
            }


            return BadRequest("Something went wrong. Please try again.");
        }
    }
}
