using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    public class CollectionController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<AppUser> _userManager;

        public CollectionController(IUnitOfWork unitOfWork, UserManager<AppUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IEnumerable<CollectionTagDto>> GetAllTags()
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            var isAdmin = await _userManager.IsInRoleAsync(user, PolicyConstants.AdminRole);
            if (isAdmin)
            {
                return await _unitOfWork.CollectionTagRepository.GetAllTagDtosAsync();    
            }
            return await _unitOfWork.CollectionTagRepository.GetAllPromotedTagDtosAsync();
        }
        
        [Authorize(Policy = "RequireAdminRole")]
        [HttpGet("search")]
        public async Task<IEnumerable<CollectionTagDto>> SearchTags(string queryString)
        {
            queryString ??= "";
            queryString = queryString.Replace(@"%", "");
            if (queryString.Length == 0) return await _unitOfWork.CollectionTagRepository.GetAllTagDtosAsync();
            
            return await _unitOfWork.CollectionTagRepository.SearchTagDtosAsync(queryString);
        }
        
        [Authorize(Policy = "RequireAdminRole")]
        [HttpPost("update")]
        public async Task<ActionResult> UpdateTag(CollectionTagDto updatedTag)
        {
            var existingTag = await _unitOfWork.CollectionTagRepository.GetTagAsync(updatedTag.Id);
            if (existingTag == null) return BadRequest("This tag does not exist");

            existingTag.Promoted = updatedTag.Promoted;
            existingTag.Title = updatedTag.Title;
            existingTag.NormalizedTitle = Parser.Parser.Normalize(updatedTag.Title).ToUpper();

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

                foreach (var seriesIdToRemove in updateSeriesForTagDto.SeriesIdsToRemove)
                {
                    tag.SeriesMetadatas.Remove(tag.SeriesMetadatas.Single(sm => sm.SeriesId == seriesIdToRemove));
                }


                if (tag.SeriesMetadatas.Count == 0)
                {
                    _unitOfWork.CollectionTagRepository.Remove(tag);
                }

                if (_unitOfWork.HasChanges() && await _unitOfWork.CommitAsync())
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