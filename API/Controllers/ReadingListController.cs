using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.DTOs.ReadingLists;
using API.Entities;
using API.Extensions;
using API.Helpers;
using API.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers
{
    public class ReadingListController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ReadingListController> _logger;
        private readonly ChapterSortComparerZeroFirst _chapterSortComparerForInChapterSorting = new ChapterSortComparerZeroFirst();

        public ReadingListController(IUnitOfWork unitOfWork, ILogger<ReadingListController> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ReadingListDto>>> GetList(int readingListId)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            return Ok(await _unitOfWork.ReadingListRepository.GetReadingListDtoByIdAsync(readingListId, user.Id));
        }

        /// <summary>
        /// Returns reading lists (paginated) for a given user.
        /// </summary>
        /// <param name="includePromoted">Defaults to true</param>
        /// <returns></returns>
        [HttpPost("lists")]
        public async Task<ActionResult<IEnumerable<ReadingListDto>>> GetListsForUser([FromQuery] UserParams userParams, [FromQuery] bool includePromoted = true)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            var items = await _unitOfWork.ReadingListRepository.GetReadingListDtosForUserAsync(user.Id, includePromoted,
                userParams);
            Response.AddPaginationHeader(items.CurrentPage, items.PageSize, items.TotalCount, items.TotalPages);

            return Ok(items);
        }

        /// <summary>
        /// Fetches all reading list items for a given list including rich metadata around series, volume, chapters, and progress
        /// </summary>
        /// <remarks>This call is expensive</remarks>
        /// <param name="readingListId"></param>
        /// <returns></returns>
        [HttpGet("items")]
        public async Task<ActionResult<IEnumerable<ReadingListItemDto>>> GetListForUser(int readingListId)
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            var items = await _unitOfWork.ReadingListRepository.GetReadingListItemDtosByIdAsync(readingListId, user.Id);

            return Ok(await _unitOfWork.ReadingListRepository.AddReadingProgressModifiers(user.Id, items.ToList()));
        }

        /// <summary>
        /// Updates an items position
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost("update-position")]
        public async Task<ActionResult> UpdateListItemPosition(UpdateReadingListPosition dto)
        {
            // Make sure UI buffers events
            var items = (await _unitOfWork.ReadingListRepository.GetReadingListItemsByIdAsync(dto.ReadingListId)).ToList();
            var item = items.Find(r => r.Id == dto.ReadingListItemId);
            items.Remove(item);
            items.Insert(dto.ToPosition, item);

            for (var i = 0; i < items.Count; i++)
            {
                items[i].Order = i;
            }

            if (_unitOfWork.HasChanges() && await _unitOfWork.CommitAsync())
            {
                return Ok("Updated");
            }

            return BadRequest("Couldn't update position");
        }

        /// <summary>
        /// Removes all entries that are fully read from the reading list
        /// </summary>
        /// <param name="readingListId"></param>
        /// <returns></returns>
        [HttpPost("remove-read")]
        public async Task<ActionResult> DeleteReadFromList([FromQuery] int readingListId)
        {
            // TODO: PERF: This takes about 400ms, clean it up.
            var user = await _unitOfWork.UserRepository.GetUserByUsernameFastAsync(User.GetUsername());
            var items = await _unitOfWork.ReadingListRepository.GetReadingListItemDtosByIdAsync(readingListId, user.Id);
            items = await _unitOfWork.ReadingListRepository.AddReadingProgressModifiers(user.Id, items.ToList());

            // Collect all Ids to remove
            var itemIdsToRemove = items.Where(item => item.PagesRead == item.PagesTotal).Select(item => item.Id);

            try
            {
                var listItems =
                    (await _unitOfWork.ReadingListRepository.GetReadingListItemsByIdAsync(readingListId)).Where(r =>
                        itemIdsToRemove.Contains(r.Id));
                _unitOfWork.ReadingListRepository.BulkRemove(listItems);

                if (_unitOfWork.HasChanges())
                {
                    await _unitOfWork.CommitAsync();
                    return Ok("Updated");
                }
                else
                {
                    return Ok("Nothing to remove");
                }
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
            }

            return BadRequest("Could not remove read items");
        }

        /// <summary>
        /// Deletes a reading list
        /// </summary>
        /// <param name="readingListId"></param>
        /// <returns></returns>
        [HttpDelete]
        public async Task<ActionResult> DeleteList([FromQuery] int readingListId)
        {
            var user = await _unitOfWork.UserRepository.GetUserWithReadingListsByUsernameAsync(User.GetUsername());
            var readingList = user.ReadingLists.SingleOrDefault(r => r.Id == readingListId);
            if (readingList == null)
            {
                return BadRequest("User is not associated with this reading list");
            }

            user.ReadingLists.Remove(readingList);

            if (_unitOfWork.HasChanges() && await _unitOfWork.CommitAsync())
            {
                return Ok("Deleted");
            }

            return BadRequest("There was an issue deleting reading list");
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

            return Ok(await _unitOfWork.ReadingListRepository.GetReadingListDtoByTitleAsync(dto.Title));
        }

        [HttpPost("update-by-series")]
        public async Task<ActionResult> UpdateListBySeries(UpdateReadingListBySeriesDto dto)
        {
            var user = await _unitOfWork.UserRepository.GetUserWithReadingListsByUsernameAsync(User.GetUsername());
            var readingList = user.ReadingLists.SingleOrDefault(l => l.Id == dto.ReadingListId);
            if (readingList == null) return BadRequest("Reading List does not exist");
            var chapterIdsForSeries =
                await _unitOfWork.SeriesRepository.GetChapterIdsForSeriesAsync(new [] {dto.SeriesId});



            readingList.Items ??= new List<ReadingListItem>();
            var lastOrder = 0;
            if (readingList.Items.Any())
            {
                lastOrder = readingList.Items.DefaultIfEmpty().Max(rli => rli.Order);
            }
            var existingChapterIds = readingList.Items.Select(rli => rli.ChapterId).ToList();
            var chaptersForSeries = (await _unitOfWork.ChapterRepository.GetChaptersByIdsAsync(chapterIdsForSeries))
                .OrderBy(c => int.Parse(c.Volume.Name))
                .ThenBy(x => double.Parse(x.Number), _chapterSortComparerForInChapterSorting);

            // If there are adds, tell tracking this has been modified
            if (AddChaptersToReadingList(dto.SeriesId, chaptersForSeries, existingChapterIds, readingList, lastOrder))
            {
                _unitOfWork.ReadingListRepository.Update(readingList);
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

        /// <summary>
        /// Adds a list of Chapters as reading list items.
        /// </summary>
        /// <param name="seriesId"></param>
        /// <param name="chaptersForSeries"></param>
        /// <param name="existingChapterIds"></param>
        /// <param name="readingList"></param>
        /// <param name="lastOrder"></param>
        /// <returns>True if new chapters were added</returns>
        private static bool AddChaptersToReadingList(int seriesId, IEnumerable<Chapter> chaptersForSeries,
            ICollection<int> existingChapterIds, ReadingList readingList, int lastOrder)
        {
            // BUG: There is an order issue with this logic
            var index = lastOrder + 1;
            foreach (var chapter in chaptersForSeries)
            {
                if (existingChapterIds.Contains(chapter.Id))
                {
                    continue;
                }

                readingList.Items.Add(new ReadingListItem()
                {
                    Order = index,
                    ChapterId = chapter.Id,
                    SeriesId = seriesId,
                    VolumeId = chapter.VolumeId
                });
                index += 1;
            }

            return index > lastOrder + 1;
        }
    }
}
