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
        public ActionResult GetListForUser(bool includePromoted = true)
        {
            return Ok();
        }

        [HttpPost("add-to-list")]
        public ActionResult AddChapterToList()
        {
            return Ok();
        }
    }
}
