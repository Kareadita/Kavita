using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs.ReadingLists;
using API.Extensions;
using API.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    public class ReadingListController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;

        public ReadingListController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// Returns reading lists for a given user.
        /// </summary>
        /// <param name="includePromoted">Defaults to true</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ReadingListDto>>> GetListForUser(bool includePromoted = true)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());

            return Ok(await _unitOfWork.ReadingListRepository.GetReadingListsForUser(user.Id, includePromoted));
        }

        [HttpPost("add-to-list")]
        public ActionResult AddChapterToList()
        {
            return Ok();
        }
    }
}
