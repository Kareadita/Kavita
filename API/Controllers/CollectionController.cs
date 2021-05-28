using System.Collections.Generic;
using System.Threading.Tasks;
using API.Constants;
using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers
{
    public class CollectionController : BaseApiController
    {
        private readonly ILogger<CollectionController> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<AppUser> _userManager;

        public CollectionController(ILogger<CollectionController> logger, IUnitOfWork unitOfWork, UserManager<AppUser> userManager)
        {
            _logger = logger;
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
            else
            {
                return await _unitOfWork.CollectionTagRepository.GetAllPromotedTagDtosAsync();
            }
            
        }
        
        [Authorize(Policy = "RequireAdminRole")]
        [HttpGet("search")]
        public async Task<IEnumerable<CollectionTagDto>> SearchTags(string queryString)
        {
            if (queryString == null)
            {
                queryString = "";
            }
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
                if (await _unitOfWork.Complete())
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
        
        
        
    }
}