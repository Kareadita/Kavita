using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.ReadingLists;
using API.Extensions;
using API.Helpers;
using API.Services;
using API.SignalR;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[Authorize]
public class ReadingListController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventHub _eventHub;
    private readonly IReadingListService _readingListService;
    private readonly IDirectoryService _directoryService;

    public ReadingListController(IUnitOfWork unitOfWork, IEventHub eventHub, IReadingListService readingListService, IDirectoryService directoryService)
    {
        _unitOfWork = unitOfWork;
        _eventHub = eventHub;
        _readingListService = readingListService;
        _directoryService = directoryService;
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
    /// <param name="includePromoted">Include Promoted Reading Lists along with user's Reading Lists. Defaults to true</param>
    /// <param name="userParams">Pagination parameters</param>
    /// <returns></returns>
    [HttpPost("lists")]
    public async Task<ActionResult<IEnumerable<ReadingListDto>>> GetListsForUser([FromQuery] UserParams userParams, bool includePromoted = true)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
        var items = await _unitOfWork.ReadingListRepository.GetReadingListDtosForUserAsync(userId, includePromoted,
            userParams);
        Response.AddPaginationHeader(items.CurrentPage, items.PageSize, items.TotalCount, items.TotalPages);

        return Ok(items);
    }

    /// <summary>
    /// Returns all Reading Lists the user has access to that have a series within it.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
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
        var items = await _unitOfWork.ReadingListRepository.GetReadingListItemDtosByIdAsync(readingListId, User.GetUserId());
        return Ok(items);
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
        var user = await _readingListService.UserHasReadingListAccess(dto.ReadingListId, User.GetUsername());
        if (user == null)
        {
            return BadRequest("You do not have permissions on this reading list or the list doesn't exist");
        }

        if (await _readingListService.UpdateReadingListItemPosition(dto)) return Ok("Updated");


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
        var user = await _readingListService.UserHasReadingListAccess(dto.ReadingListId, User.GetUsername());
        if (user == null)
        {
            return BadRequest("You do not have permissions on this reading list or the list doesn't exist");
        }

        if (await _readingListService.DeleteReadingListItem(dto))
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
        var user = await _readingListService.UserHasReadingListAccess(readingListId, User.GetUsername());
        if (user == null)
        {
            return BadRequest("You do not have permissions on this reading list or the list doesn't exist");
        }

        if (await _readingListService.RemoveFullyReadItems(readingListId, user))
        {
            return Ok("Updated");
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
        var user = await _readingListService.UserHasReadingListAccess(readingListId, User.GetUsername());
        if (user == null)
        {
            return BadRequest("You do not have permissions on this reading list or the list doesn't exist");
        }

        if (await _readingListService.DeleteReadingList(readingListId, user)) return Ok("List was deleted");

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
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.ReadingLists);
        if (user == null) return Unauthorized();

        try
        {
            await _readingListService.CreateReadingListForUser(user, dto.Title);
        }
        catch (KavitaException ex)
        {
            return BadRequest(ex.Message);
        }

        return Ok(await _unitOfWork.ReadingListRepository.GetReadingListDtoByTitleAsync(user.Id, dto.Title));
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

        var user = await _readingListService.UserHasReadingListAccess(readingList.Id, User.GetUsername());
        if (user == null)
        {
            return BadRequest("You do not have permissions on this reading list or the list doesn't exist");
        }

        try
        {
            await _readingListService.UpdateReadingList(readingList, dto);
        }
        catch (KavitaException ex)
        {
            return BadRequest(ex.Message);
        }

        return Ok("Updated");
    }

    /// <summary>
    /// Adds all chapters from a Series to a reading list
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update-by-series")]
    public async Task<ActionResult> UpdateListBySeries(UpdateReadingListBySeriesDto dto)
    {
        var user = await _readingListService.UserHasReadingListAccess(dto.ReadingListId, User.GetUsername());
        if (user == null)
        {
            return BadRequest("You do not have permissions on this reading list or the list doesn't exist");
        }

        var readingList = user.ReadingLists.SingleOrDefault(l => l.Id == dto.ReadingListId);
        if (readingList == null) return BadRequest("Reading List does not exist");
        var chapterIdsForSeries =
            await _unitOfWork.SeriesRepository.GetChapterIdsForSeriesAsync(new [] {dto.SeriesId});

        // If there are adds, tell tracking this has been modified
        if (await _readingListService.AddChaptersToReadingList(dto.SeriesId, chapterIdsForSeries, readingList))
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
        var user = await _readingListService.UserHasReadingListAccess(dto.ReadingListId, User.GetUsername());
        if (user == null)
        {
            return BadRequest("You do not have permissions on this reading list or the list doesn't exist");
        }
        var readingList = user.ReadingLists.SingleOrDefault(l => l.Id == dto.ReadingListId);
        if (readingList == null) return BadRequest("Reading List does not exist");

        var chapterIds = await _unitOfWork.VolumeRepository.GetChapterIdsByVolumeIds(dto.VolumeIds);
        foreach (var chapterId in dto.ChapterIds)
        {
            chapterIds.Add(chapterId);
        }

        // If there are adds, tell tracking this has been modified
        if (await _readingListService.AddChaptersToReadingList(dto.SeriesId, chapterIds, readingList))
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
        var user = await _readingListService.UserHasReadingListAccess(dto.ReadingListId, User.GetUsername());
        if (user == null)
        {
            return BadRequest("You do not have permissions on this reading list or the list doesn't exist");
        }
        var readingList = user.ReadingLists.SingleOrDefault(l => l.Id == dto.ReadingListId);
        if (readingList == null) return BadRequest("Reading List does not exist");

        var ids = await _unitOfWork.SeriesRepository.GetChapterIdWithSeriesIdForSeriesAsync(dto.SeriesIds.ToArray());

        foreach (var seriesId in ids.Keys)
        {
            // If there are adds, tell tracking this has been modified
            if (await _readingListService.AddChaptersToReadingList(seriesId, ids[seriesId], readingList))
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
        var user = await _readingListService.UserHasReadingListAccess(dto.ReadingListId, User.GetUsername());
        if (user == null)
        {
            return BadRequest("You do not have permissions on this reading list or the list doesn't exist");
        }
        var readingList = user.ReadingLists.SingleOrDefault(l => l.Id == dto.ReadingListId);
        if (readingList == null) return BadRequest("Reading List does not exist");

        var chapterIdsForVolume =
            (await _unitOfWork.ChapterRepository.GetChaptersAsync(dto.VolumeId)).Select(c => c.Id).ToList();

        // If there are adds, tell tracking this has been modified
        if (await _readingListService.AddChaptersToReadingList(dto.SeriesId, chapterIdsForVolume, readingList))
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
        var user = await _readingListService.UserHasReadingListAccess(dto.ReadingListId, User.GetUsername());
        if (user == null)
        {
            return BadRequest("You do not have permissions on this reading list or the list doesn't exist");
        }
        var readingList = user.ReadingLists.SingleOrDefault(l => l.Id == dto.ReadingListId);
        if (readingList == null) return BadRequest("Reading List does not exist");

        // If there are adds, tell tracking this has been modified
        if (await _readingListService.AddChaptersToReadingList(dto.SeriesId, new List<int>() { dto.ChapterId }, readingList))
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
    /// Returns a list of characters associated with the reading list
    /// </summary>
    /// <param name="readingListId"></param>
    /// <returns></returns>
    [HttpGet("characters")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.TenMinute)]
    public ActionResult<IEnumerable<PersonDto>> GetCharactersForList(int readingListId)
    {
        return Ok(_unitOfWork.ReadingListRepository.GetReadingListCharactersAsync(readingListId));
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

    /// <summary>
    /// Checks if a reading list exists with the name
    /// </summary>
    /// <param name="name">If empty or null, will return true as that is invalid</param>
    /// <returns></returns>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpGet("name-exists")]
    public async Task<ActionResult<bool>> DoesNameExists(string name)
    {
        if (string.IsNullOrEmpty(name)) return true;
        return Ok(await _unitOfWork.ReadingListRepository.ReadingListExists(name));
    }
}
