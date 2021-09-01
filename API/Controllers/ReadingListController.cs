using System.Collections.Generic;
using API.DTOs.ReadingLists;
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
        public ActionResult<IEnumerable<ReadingListDto>> GetListForUser(bool includePromoted = true)
        {
            return Ok(new ReadingListDto[] {});
        }

        [HttpPost("add-to-list")]
        public ActionResult AddChapterToList()
        {
            return Ok();
        }
    }
}
