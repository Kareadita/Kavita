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

#nullable enable

[Authorize]
public class ReadingListController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReadingListService _readingListService;
    private readonly ILocalizationService _localizationService;

    public ReadingListController(IUnitOfWork unitOfWork, IReadingListService readingListService,
        ILocalizationService localizationService)
    {
        _unitOfWork = unitOfWork;
        _readingListService = readingListService;
        _localizationService = localizationService;
    }

    /// <summary>
    /// Fetches a single Reading List
    /// </summary>
    /// <param name="readingListId"></param>
    /// <returns></returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ReadingListDto>>> GetList(int readingListId)
    {
        return Ok(await _unitOfWork.ReadingListRepository.GetReadingListDtoByIdAsync(readingListId, User.GetUserId()));
    }

    /// <summary>
    /// Returns reading lists (paginated) for a given user.
    /// </summary>
    /// <param name="includePromoted">Include Promoted Reading Lists along with user's Reading Lists. Defaults to true</param>
    /// <param name="userParams">Pagination parameters</param>
    /// <param name="sortByLastModified">Sort by last modified (most recent first) or by title (alphabetical)</param>
    /// <returns></returns>
    [HttpPost("lists")]
    public async Task<ActionResult<IEnumerable<ReadingListDto>>> GetListsForUser([FromQuery] UserParams userParams,
        bool includePromoted = true, bool sortByLastModified = false)
    {
        var items = await _unitOfWork.ReadingListRepository.GetReadingListDtosForUserAsync(User.GetUserId(), includePromoted,
            userParams, sortByLastModified);
        Response.AddPaginationHeader(items.CurrentPage, items.PageSize, items.TotalCount, items.TotalPages);

        return Ok(items);
    }

    /// <summary>
    /// Returns all Reading Lists the user has access to that the given series within it.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpGet("lists-for-series")]
    public async Task<ActionResult<IEnumerable<ReadingListDto>>> GetListsForSeries(int seriesId)
    {
        return Ok(await _unitOfWork.ReadingListRepository.GetReadingListDtosForSeriesAndUserAsync(User.GetUserId(),
            seriesId, true));
    }

    /// <summary>
    /// Returns all Reading Lists the user has access to that has the given chapter within it.
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpGet("lists-for-chapter")]
    public async Task<ActionResult<IEnumerable<ReadingListDto>>> GetListsForChapter(int chapterId)
    {
        return Ok(await _unitOfWork.ReadingListRepository.GetReadingListDtosForChapterAndUserAsync(User.GetUserId(),
            chapterId, true));
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
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));
        // Make sure UI buffers events
        var user = await _readingListService.UserHasReadingListAccess(dto.ReadingListId, User.GetUsername());
        if (user == null)
        {
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "reading-list-permission"));
        }

        if (await _readingListService.UpdateReadingListItemPosition(dto)) return Ok(await _localizationService.Translate(User.GetUserId(), "reading-list-updated"));


        return BadRequest(await _localizationService.Translate(User.GetUserId(), "reading-list-position"));
    }

    /// <summary>
    /// Deletes a list item from the list. Will reorder all item positions afterwards
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("delete-item")]
    public async Task<ActionResult> DeleteListItem(UpdateReadingListPosition dto)
    {
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));
        var user = await _readingListService.UserHasReadingListAccess(dto.ReadingListId, User.GetUsername());
        if (user == null)
        {
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "reading-list-permission"));
        }

        if (await _readingListService.DeleteReadingListItem(dto))
        {
            return Ok(await _localizationService.Translate(User.GetUserId(), "reading-list-updated"));
        }

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "reading-list-item-delete"));
    }

    /// <summary>
    /// Removes all entries that are fully read from the reading list
    /// </summary>
    /// <param name="readingListId"></param>
    /// <returns></returns>
    [HttpPost("remove-read")]
    public async Task<ActionResult> DeleteReadFromList([FromQuery] int readingListId)
    {
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));

        var user = await _readingListService.UserHasReadingListAccess(readingListId, User.GetUsername());
        if (user == null)
        {
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "reading-list-permission"));
        }

        if (await _readingListService.RemoveFullyReadItems(readingListId, user))
        {
            return Ok(await _localizationService.Translate(User.GetUserId(), "reading-list-updated"));
        }

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "reading-list-item-delete"));
    }

    /// <summary>
    /// Deletes a reading list
    /// </summary>
    /// <param name="readingListId"></param>
    /// <returns></returns>
    [HttpDelete]
    public async Task<ActionResult> DeleteList([FromQuery] int readingListId)
    {
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));
        var user = await _readingListService.UserHasReadingListAccess(readingListId, User.GetUsername());
        if (user == null)
        {
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "reading-list-permission"));
        }

        if (await _readingListService.DeleteReadingList(readingListId, user))
            return Ok(await _localizationService.Translate(User.GetUserId(), "reading-list-deleted"));

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-reading-list-delete"));
    }

    /// <summary>
    /// Creates a new List with a unique title. Returns the new ReadingList back
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("create")]
    public async Task<ActionResult<ReadingListDto>> CreateList(CreateReadingListDto dto)
    {
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.ReadingLists);
        if (user == null) return Unauthorized();

        try
        {
            await _readingListService.CreateReadingListForUser(user, dto.Title);
        }
        catch (KavitaException ex)
        {
            return BadRequest(await _localizationService.Translate(User.GetUserId(), ex.Message));
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
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));
        var readingList = await _unitOfWork.ReadingListRepository.GetReadingListByIdAsync(dto.ReadingListId);
        if (readingList == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "reading-list-doesnt-exist"));

        var user = await _readingListService.UserHasReadingListAccess(readingList.Id, User.GetUsername());
        if (user == null)
        {
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "reading-list-permission"));
        }

        try
        {
            await _readingListService.UpdateReadingList(readingList, dto);
        }
        catch (KavitaException ex)
        {
            return BadRequest(await _localizationService.Translate(User.GetUserId(), ex.Message));
        }

        return Ok(await _localizationService.Translate(User.GetUserId(), "reading-list-updated"));
    }

    /// <summary>
    /// Adds all chapters from a Series to a reading list
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update-by-series")]
    public async Task<ActionResult> UpdateListBySeries(UpdateReadingListBySeriesDto dto)
    {
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));
        var user = await _readingListService.UserHasReadingListAccess(dto.ReadingListId, User.GetUsername());
        if (user == null)
        {
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "reading-list-permission"));
        }

        var readingList = user.ReadingLists.SingleOrDefault(l => l.Id == dto.ReadingListId);
        if (readingList == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "reading-list-doesnt-exist"));
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
                return Ok(await _localizationService.Translate(User.GetUserId(), "reading-list-updated"));
            }
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
        }

        return Ok(await _localizationService.Translate(User.GetUserId(), "nothing-to-do"));
    }


    /// <summary>
    /// Adds all chapters from a list of volumes and chapters to a reading list
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update-by-multiple")]
    public async Task<ActionResult> UpdateListByMultiple(UpdateReadingListByMultipleDto dto)
    {
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));
        var user = await _readingListService.UserHasReadingListAccess(dto.ReadingListId, User.GetUsername());
        if (user == null)
        {
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "reading-list-permission"));
        }
        var readingList = user.ReadingLists.SingleOrDefault(l => l.Id == dto.ReadingListId);
        if (readingList == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "reading-list-doesnt-exist"));

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
                return Ok(await _localizationService.Translate(User.GetUserId(), "reading-list-updated"));
            }
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
        }

        return Ok(await _localizationService.Translate(User.GetUserId(), "nothing-to-do"));
    }

    /// <summary>
    /// Adds all chapters from a list of series to a reading list
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update-by-multiple-series")]
    public async Task<ActionResult> UpdateListByMultipleSeries(UpdateReadingListByMultipleSeriesDto dto)
    {
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));
        var user = await _readingListService.UserHasReadingListAccess(dto.ReadingListId, User.GetUsername());
        if (user == null)
        {
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "reading-list-permission"));
        }
        var readingList = user.ReadingLists.SingleOrDefault(l => l.Id == dto.ReadingListId);
        if (readingList == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "reading-list-doesnt-exist"));

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
                return Ok(await _localizationService.Translate(User.GetUserId(), "reading-list-updated"));
            }
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
        }

        return Ok(await _localizationService.Translate(User.GetUserId(), "nothing-to-do"));
    }

    [HttpPost("update-by-volume")]
    public async Task<ActionResult> UpdateListByVolume(UpdateReadingListByVolumeDto dto)
    {
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));
        var user = await _readingListService.UserHasReadingListAccess(dto.ReadingListId, User.GetUsername());
        if (user == null)
        {
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "reading-list-permission"));
        }
        var readingList = user.ReadingLists.SingleOrDefault(l => l.Id == dto.ReadingListId);
        if (readingList == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "reading-list-doesnt-exist"));

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
                return Ok(await _localizationService.Translate(User.GetUserId(), "reading-list-updated"));
            }
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
        }

        return Ok(await _localizationService.Translate(User.GetUserId(), "nothing-to-do"));
    }

    [HttpPost("update-by-chapter")]
    public async Task<ActionResult> UpdateListByChapter(UpdateReadingListByChapterDto dto)
    {
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));
        var user = await _readingListService.UserHasReadingListAccess(dto.ReadingListId, User.GetUsername());
        if (user == null)
        {
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "reading-list-permission"));
        }
        var readingList = user.ReadingLists.SingleOrDefault(l => l.Id == dto.ReadingListId);
        if (readingList == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "reading-list-doesnt-exist"));

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
                return Ok(await _localizationService.Translate(User.GetUserId(), "reading-list-updated"));
            }
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
        }

        return Ok(await _localizationService.Translate(User.GetUserId(), "nothing-to-do"));
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
        if (readingListItem == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "chapter-doesnt-exist"));
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
        if (readingListItem == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "chapter-doesnt-exist"));
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



    /// <summary>
    /// Promote/UnPromote multiple reading lists in one go. Will only update the authenticated user's reading lists and will only work if the user has promotion role
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("promote-multiple")]
    public async Task<ActionResult> PromoteMultipleReadingLists(PromoteReadingListsDto dto)
    {
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));

        // This needs to take into account owner as I can select other users cards
        var userId = User.GetUserId();
        if (!User.IsInRole(PolicyConstants.PromoteRole) && !User.IsInRole(PolicyConstants.AdminRole))
        {
            return BadRequest(await _localizationService.Translate(userId, "permission-denied"));
        }

        var readingLists = await _unitOfWork.ReadingListRepository.GetReadingListsByIds(dto.ReadingListIds);

        foreach (var readingList in readingLists)
        {
            if (readingList.AppUserId != userId) continue;
            readingList.Promoted = dto.Promoted;
            _unitOfWork.ReadingListRepository.Update(readingList);
        }

        if (!_unitOfWork.HasChanges()) return Ok();
        await _unitOfWork.CommitAsync();

        return Ok();
    }


    /// <summary>
    /// Delete multiple reading lists in one go
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("delete-multiple")]
    public async Task<ActionResult> DeleteMultipleReadingLists(DeleteReadingListsDto dto)
    {
        // This needs to take into account owner as I can select other users cards
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(User.GetUserId(), AppUserIncludes.ReadingLists);
        if (user == null) return Unauthorized();

        user.ReadingLists = user.ReadingLists.Where(uc => !dto.ReadingListIds.Contains(uc.Id)).ToList();
        _unitOfWork.UserRepository.Update(user);


        if (!_unitOfWork.HasChanges()) return Ok();
        await _unitOfWork.CommitAsync();

        return Ok();
    }
}
