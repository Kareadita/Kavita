﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.Data;
using API.DTOs.ReadingLists;
using API.Entities;
using API.Extensions;
using API.Helpers;
using API.SignalR;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    public class ReadingListController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEventHub _eventHub;
        private readonly ChapterSortComparerZeroFirst _chapterSortComparerForInChapterSorting = new ChapterSortComparerZeroFirst();

        public ReadingListController(IUnitOfWork unitOfWork, IEventHub eventHub)
        {
            _unitOfWork = unitOfWork;
            _eventHub = eventHub;
        }

        /// <summary>
        /// Fetches a single Reading List
        /// </summary>
        /// <param name="readingListId"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ReadingListDto>>> GetList(int readingListId)
        {
            var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
            return Ok(await _unitOfWork.ReadingListRepository.GetReadingListDtoByIdAsync(readingListId, userId));
        }

        /// <summary>
        /// Returns reading lists (paginated) for a given user.
        /// </summary>
        /// <param name="includePromoted">Defaults to true</param>
        /// <returns></returns>
        [HttpPost("lists")]
        public async Task<ActionResult<IEnumerable<ReadingListDto>>> GetListsForUser([FromQuery] UserParams userParams, [FromQuery] bool includePromoted = true)
        {
            var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
            var items = await _unitOfWork.ReadingListRepository.GetReadingListDtosForUserAsync(userId, includePromoted,
                userParams);
            Response.AddPaginationHeader(items.CurrentPage, items.PageSize, items.TotalCount, items.TotalPages);

            return Ok(items);
        }

        [HttpGet("lists-for-series")]
        public async Task<ActionResult<IEnumerable<ReadingListDto>>> GetListsForSeries(int seriesId)
        {
            var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
            var items = await _unitOfWork.ReadingListRepository.GetReadingListDtosForSeriesAndUserAsync(userId, seriesId, true);

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
            var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
            var items = await _unitOfWork.ReadingListRepository.GetReadingListItemDtosByIdAsync(readingListId, userId);

            return Ok(await _unitOfWork.ReadingListRepository.AddReadingProgressModifiers(userId, items.ToList()));
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
        /// Deletes a list item from the list. Will reorder all item positions afterwards
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost("delete-item")]
        public async Task<ActionResult> DeleteListItem(UpdateReadingListPosition dto)
        {
            var readingList = await _unitOfWork.ReadingListRepository.GetReadingListByIdAsync(dto.ReadingListId);
            readingList.Items = readingList.Items.Where(r => r.Id != dto.ReadingListItemId).ToList();


            var index = 0;
            foreach (var readingListItem in readingList.Items)
            {
                readingListItem.Order = index;
                index++;
            }

            if (!_unitOfWork.HasChanges()) return Ok();

            if (await _unitOfWork.CommitAsync())
            {
                return Ok("Updated");
            }

            return BadRequest("Couldn't delete item");
        }

        /// <summary>
        /// Removes all entries that are fully read from the reading list
        /// </summary>
        /// <param name="readingListId"></param>
        /// <returns></returns>
        [HttpPost("remove-read")]
        public async Task<ActionResult> DeleteReadFromList([FromQuery] int readingListId)
        {
            var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
            var items = await _unitOfWork.ReadingListRepository.GetReadingListItemDtosByIdAsync(readingListId, userId);
            items = await _unitOfWork.ReadingListRepository.AddReadingProgressModifiers(userId, items.ToList());

            // Collect all Ids to remove
            var itemIdsToRemove = items.Where(item => item.PagesRead == item.PagesTotal).Select(item => item.Id);

            try
            {
                var listItems =
                    (await _unitOfWork.ReadingListRepository.GetReadingListItemsByIdAsync(readingListId)).Where(r =>
                        itemIdsToRemove.Contains(r.Id));
                _unitOfWork.ReadingListRepository.BulkRemove(listItems);

                if (!_unitOfWork.HasChanges()) return Ok("Nothing to remove");

                await _unitOfWork.CommitAsync();
                return Ok("Updated");
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
            var isAdmin = await _unitOfWork.UserRepository.IsUserAdminAsync(user);
            var readingList = user.ReadingLists.SingleOrDefault(r => r.Id == readingListId);
            if (readingList == null && !isAdmin)
            {
                return BadRequest("User is not associated with this reading list");
            }

            readingList = await _unitOfWork.ReadingListRepository.GetReadingListByIdAsync(readingListId);

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

            user.ReadingLists.Add(DbFactory.ReadingList(dto.Title, string.Empty, false));

            if (!_unitOfWork.HasChanges()) return BadRequest("There was a problem creating list");

            await _unitOfWork.CommitAsync();

            return Ok(await _unitOfWork.ReadingListRepository.GetReadingListDtoByTitleAsync(dto.Title));
        }

        /// <summary>
        /// Update the properties (title, summary) of a reading list
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost("update")]
        public async Task<ActionResult> UpdateList(UpdateReadingListDto dto)
        {
            var readingList = await _unitOfWork.ReadingListRepository.GetReadingListByIdAsync(dto.ReadingListId);
            if (readingList == null) return BadRequest("List does not exist");



            if (!string.IsNullOrEmpty(dto.Title))
            {
                readingList.Title = dto.Title; // Should I check if this is unique?
                readingList.NormalizedTitle = Parser.Parser.Normalize(readingList.Title);
            }
            if (!string.IsNullOrEmpty(dto.Title))
            {
                readingList.Summary = dto.Summary;
            }

            readingList.Promoted = dto.Promoted;

            readingList.CoverImageLocked = dto.CoverImageLocked;

            if (!dto.CoverImageLocked)
            {
                readingList.CoverImageLocked = false;
                readingList.CoverImage = string.Empty;
                await _eventHub.SendMessageAsync(MessageFactory.CoverUpdate,
                    MessageFactory.CoverUpdateEvent(readingList.Id, MessageFactoryEntityTypes.ReadingList), false);
                _unitOfWork.ReadingListRepository.Update(readingList);
            }



            _unitOfWork.ReadingListRepository.Update(readingList);

            if (await _unitOfWork.CommitAsync())
            {
                return Ok("Updated");
            }
            return BadRequest("Could not update reading list");
        }

        /// <summary>
        /// Adds all chapters from a Series to a reading list
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost("update-by-series")]
        public async Task<ActionResult> UpdateListBySeries(UpdateReadingListBySeriesDto dto)
        {
            var user = await _unitOfWork.UserRepository.GetUserWithReadingListsByUsernameAsync(User.GetUsername());
            var readingList = user.ReadingLists.SingleOrDefault(l => l.Id == dto.ReadingListId);
            if (readingList == null) return BadRequest("Reading List does not exist");
            var chapterIdsForSeries =
                await _unitOfWork.SeriesRepository.GetChapterIdsForSeriesAsync(new [] {dto.SeriesId});

            // If there are adds, tell tracking this has been modified
            if (await AddChaptersToReadingList(dto.SeriesId, chapterIdsForSeries, readingList))
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
        /// Adds all chapters from a list of volumes and chapters to a reading list
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost("update-by-multiple")]
        public async Task<ActionResult> UpdateListByMultiple(UpdateReadingListByMultipleDto dto)
        {
            var user = await _unitOfWork.UserRepository.GetUserWithReadingListsByUsernameAsync(User.GetUsername());
            var readingList = user.ReadingLists.SingleOrDefault(l => l.Id == dto.ReadingListId);
            if (readingList == null) return BadRequest("Reading List does not exist");

            var chapterIds = await _unitOfWork.VolumeRepository.GetChapterIdsByVolumeIds(dto.VolumeIds);
            foreach (var chapterId in dto.ChapterIds)
            {
                chapterIds.Add(chapterId);
            }

            // If there are adds, tell tracking this has been modified
            if (await AddChaptersToReadingList(dto.SeriesId, chapterIds, readingList))
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
        /// Adds all chapters from a list of series to a reading list
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost("update-by-multiple-series")]
        public async Task<ActionResult> UpdateListByMultipleSeries(UpdateReadingListByMultipleSeriesDto dto)
        {
            var user = await _unitOfWork.UserRepository.GetUserWithReadingListsByUsernameAsync(User.GetUsername());
            var readingList = user.ReadingLists.SingleOrDefault(l => l.Id == dto.ReadingListId);
            if (readingList == null) return BadRequest("Reading List does not exist");

            var ids = await _unitOfWork.SeriesRepository.GetChapterIdWithSeriesIdForSeriesAsync(dto.SeriesIds.ToArray());

            foreach (var seriesId in ids.Keys)
            {
                // If there are adds, tell tracking this has been modified
                if (await AddChaptersToReadingList(seriesId, ids[seriesId], readingList))
                {
                    _unitOfWork.ReadingListRepository.Update(readingList);
                }
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

        [HttpPost("update-by-volume")]
        public async Task<ActionResult> UpdateListByVolume(UpdateReadingListByVolumeDto dto)
        {
            var user = await _unitOfWork.UserRepository.GetUserWithReadingListsByUsernameAsync(User.GetUsername());
            var readingList = user.ReadingLists.SingleOrDefault(l => l.Id == dto.ReadingListId);
            if (readingList == null) return BadRequest("Reading List does not exist");
            var chapterIdsForVolume =
                (await _unitOfWork.ChapterRepository.GetChaptersAsync(dto.VolumeId)).Select(c => c.Id).ToList();

            // If there are adds, tell tracking this has been modified
            if (await AddChaptersToReadingList(dto.SeriesId, chapterIdsForVolume, readingList))
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

        [HttpPost("update-by-chapter")]
        public async Task<ActionResult> UpdateListByChapter(UpdateReadingListByChapterDto dto)
        {
            var user = await _unitOfWork.UserRepository.GetUserWithReadingListsByUsernameAsync(User.GetUsername());
            var readingList = user.ReadingLists.SingleOrDefault(l => l.Id == dto.ReadingListId);
            if (readingList == null) return BadRequest("Reading List does not exist");

            // If there are adds, tell tracking this has been modified
            if (await AddChaptersToReadingList(dto.SeriesId, new List<int>() { dto.ChapterId }, readingList))
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
        /// Adds a list of Chapters as reading list items to the passed reading list.
        /// </summary>
        /// <param name="seriesId"></param>
        /// <param name="chapterIds"></param>
        /// <param name="readingList"></param>
        /// <returns>True if new chapters were added</returns>
        private async Task<bool> AddChaptersToReadingList(int seriesId, IList<int> chapterIds,
            ReadingList readingList)
        {
            readingList.Items ??= new List<ReadingListItem>();
            var lastOrder = 0;
            if (readingList.Items.Any())
            {
                lastOrder = readingList.Items.DefaultIfEmpty().Max(rli => rli.Order);
            }

            var existingChapterExists = readingList.Items.Select(rli => rli.ChapterId).ToHashSet();
            var chaptersForSeries = (await _unitOfWork.ChapterRepository.GetChaptersByIdsAsync(chapterIds))
                .OrderBy(c => float.Parse(c.Volume.Name))
                .ThenBy(x => double.Parse(x.Number), _chapterSortComparerForInChapterSorting);

            var index = lastOrder + 1;
            foreach (var chapter in chaptersForSeries)
            {
                if (existingChapterExists.Contains(chapter.Id)) continue;
                readingList.Items.Add(DbFactory.ReadingListItem(index, seriesId, chapter.VolumeId, chapter.Id));
                index += 1;
            }

            return index > lastOrder + 1;
        }

        /// <summary>
        /// Returns the next chapter within the reading list
        /// </summary>
        /// <param name="currentChapterId"></param>
        /// <param name="readingListId"></param>
        /// <returns>Chapter Id for next item, -1 if nothing exists</returns>
        [HttpGet("next-chapter")]
        public async Task<ActionResult<int>> GetNextChapter(int currentChapterId, int readingListId)
        {
            var items = (await _unitOfWork.ReadingListRepository.GetReadingListItemsByIdAsync(readingListId)).ToList();
            var readingListItem = items.SingleOrDefault(rl => rl.ChapterId == currentChapterId);
            if (readingListItem == null) return BadRequest("Id does not exist");
            var index = items.IndexOf(readingListItem) + 1;
            if (items.Count > index)
            {
                return items[index].ChapterId;
            }

            return Ok(-1);
        }

        /// <summary>
        /// Returns the prev chapter within the reading list
        /// </summary>
        /// <param name="currentChapterId"></param>
        /// <param name="readingListId"></param>
        /// <returns>Chapter Id for next item, -1 if nothing exists</returns>
        [HttpGet("prev-chapter")]
        public async Task<ActionResult<int>> GetPrevChapter(int currentChapterId, int readingListId)
        {
            var items = (await _unitOfWork.ReadingListRepository.GetReadingListItemsByIdAsync(readingListId)).ToList();
            var readingListItem = items.SingleOrDefault(rl => rl.ChapterId == currentChapterId);
            if (readingListItem == null) return BadRequest("Id does not exist");
            var index = items.IndexOf(readingListItem) - 1;
            if (0 <= index)
            {
                return items[index].ChapterId;
            }

            return Ok(-1);
        }
    }
}
