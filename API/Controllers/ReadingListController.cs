using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.ReadingLists;
using API.Entities;
using API.Extensions;
using API.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers
{
    public class ReadingListController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ReadingListController> _logger;

        public ReadingListController(IUnitOfWork unitOfWork, ILogger<ReadingListController> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
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

            return Ok(await _unitOfWork.ReadingListRepository.GetReadingListDtosForUserAsync(user.Id, includePromoted));
        }

        /// <summary>
        /// Creates a new List with a unique title. Returns the new ReadingList back
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost("create")]
        public async Task<ActionResult<ReadingListDto>> CreateList(CreateReadingListDto dto)
        {
            var user = await _unitOfWork.UserRepository.GetUserWithReadingListsByUsernameAsync(User.GetUsername());

            // When creating, we need to make sure Title is unique
            var hasExisting = user.ReadingLists.Any(l => l.Title.Equals(dto.Title));
            if (hasExisting)
            {
                return BadRequest("A list of this name already exists");
            }
            user.ReadingLists.Add(new ReadingList()
            {
                Promoted = false,
                Title = dto.Title,
                Summary = string.Empty
            });

            if (!_unitOfWork.HasChanges()) return BadRequest("There was a problem creating list");

            await _unitOfWork.CommitAsync();
            return Ok(new ReadingListDto()
            {
                Promoted = false,
                Title = dto.Title,
                Summary = string.Empty
            });
        }

        [HttpPost("update-by-series")]
        public async Task<ActionResult> UpdateListBySeries(UpdateReadingListBySeriesDto dto)
        {
            var readingList = await _unitOfWork.ReadingListRepository.GetReadingListByIdAsync(dto.ReadingListId);
            var chaptersForSeries =
                await _unitOfWork.SeriesRepository.GetChapterIdsForSeriesAsync(new [] {dto.SeriesId}); // This is really slow

            // This should never happen
            if (readingList == null) return BadRequest("Reading List does not exist");
            readingList.Items ??= new List<ReadingListItem>();
            var lastOrder = 0;
            if (readingList.Items.Any())
            {
                lastOrder = readingList.Items.DefaultIfEmpty().Max(rli => rli.Order);
            }
            var existingChapterIds = readingList.Items.Select(rli => rli.ChapterId).ToList();

            var index = 1;
            foreach (var chapterId in chaptersForSeries)
            {
                if (existingChapterIds.Contains(chapterId))
                {
                    continue;
                }
                readingList.Items.Add(new ReadingListItem()
                {
                    Order = lastOrder + index,
                    ChapterId = chapterId,
                    SeriesId = dto.SeriesId,
                });
                index += 1;
            }

            try
            {
                if (_unitOfWork.HasChanges())
                {
                    await _unitOfWork.CommitAsync();
                    return Ok("Updated");
                }
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
            }

            return Ok("Nothing to do");
        }
    }
}
