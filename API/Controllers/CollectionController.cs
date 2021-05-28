using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
using API.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers
{
    public class CollectionController : BaseApiController
    {
        private readonly ILogger<CollectionController> _logger;
        private readonly IUnitOfWork _unitOfWork;

        public CollectionController(ILogger<CollectionController> logger, IUnitOfWork unitOfWork)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
        }

        [HttpGet]
        public async Task<IEnumerable<CollectionTagDto>> GetAllTags()
        {
            return await _unitOfWork.CollectionTagRepository.GetAllTagDtos();
        }
        
        [HttpGet("search")]
        public async Task<IEnumerable<CollectionTagDto>> SearchTags(string queryString)
        {
            if (queryString == null)
            {
                queryString = "";
            }
            queryString = queryString.Replace(@"%", "");
            if (queryString.Length == 0) return await _unitOfWork.CollectionTagRepository.GetAllTagDtos();
            
            return await _unitOfWork.CollectionTagRepository.SearchTagDtos(queryString);
        }
        
        
    }
}