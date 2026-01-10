using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Person;
using API.DTOs.ReadingLists;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using API.Middleware;
using API.Services;
using API.Services.Reading;
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
    private readonly IReaderService _readerService;

    public ReadingListController(IUnitOfWork unitOfWork, IReadingListService readingListService,
        ILocalizationService localizationService, IReaderService readerService)
    {
        _unitOfWork = unitOfWork;
        _readingListService = readingListService;
        _localizationService = localizationService;
        _readerService = readerService;
    }

    /// <summary>
    /// Fetches a single Reading List
    /// </summary>
    /// <param name="readingListId"></param>
    /// <returns></returns>
    [HttpGet]
    public async Task<ActionResult<ReadingListDto>> GetList(int readingListId)
    {
        var readingList = await _unitOfWork.ReadingListRepository.GetReadingListDtoByIdAsync(readingListId, UserId);
        if (readingList == null)
        {
            return BadRequest(await _localizationService.Translate(UserId, "reading-list-restricted"));
        }

        return Ok(readingList);
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
        var items = await _unitOfWork.ReadingListRepository.GetReadingListDtosForUserAsync(UserId, includePromoted,
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
        return Ok(await _unitOfWork.ReadingListRepository.GetReadingListDtosForSeriesAndUserAsync(UserId,
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
        return Ok(await _unitOfWork.ReadingListRepository.GetReadingListDtosForChapterAndUserAsync(UserId,
            chapterId, true));
    }

    /// <summary>
    /// Fetches all reading list items for a given list including rich metadata around series, volume, chapters, and progress
    /// </summary>
    /// <remarks>This call is expensive</remarks>
    /// <param name="readingListId"></param>
    /// <returns></returns>
    [HttpGet("items")]
    public async Task<ActionResult<IList<ReadingListItemDto>>> GetListForUser(int readingListId)
    {
        return Ok(await _readingListService.GetReadingListItems(readingListId, UserId));
    }


    /// <summary>
    /// Updates an items position
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update-position")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> UpdateListItemPosition(UpdateReadingListPosition dto)
    {
        // Make sure UI buffers events
        var user = await _readingListService.UserHasReadingListAccess(dto.ReadingListId, Username!);
        if (user == null)
        {
            return BadRequest(await _localizationService.Translate(UserId, "reading-list-permission"));
        }

        if (await _readingListService.UpdateReadingListItemPosition(dto)) return Ok(await _localizationService.Translate(UserId, "reading-list-updated"));


        return BadRequest(await _localizationService.Translate(UserId, "reading-list-position"));
    }

    /// <summary>
    /// Deletes a list item from the list. Item orders will update as a result.
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("delete-item")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> DeleteListItem(UpdateReadingListPosition dto)
    {
        var user = await _readingListService.UserHasReadingListAccess(dto.ReadingListId, Username!);
        if (user == null)
        {
            return BadRequest(await _localizationService.Translate(UserId, "reading-list-permission"));
        }

        if (await _readingListService.DeleteReadingListItem(dto))
        {
            return Ok(await _localizationService.Translate(UserId, "reading-list-updated"));
        }

        return BadRequest(await _localizationService.Translate(UserId, "reading-list-item-delete"));
    }

    /// <summary>
    /// Removes all entries that are fully read from the reading list
    /// </summary>
    /// <param name="readingListId"></param>
    /// <returns></returns>
    [HttpPost("remove-read")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> DeleteReadFromList([FromQuery] int readingListId)
    {
        var user = await _readingListService.UserHasReadingListAccess(readingListId, Username!);
        if (user == null)
        {
            return BadRequest(await _localizationService.Translate(UserId, "reading-list-permission"));
        }

        if (await _readingListService.RemoveFullyReadItems(readingListId, user))
        {
            return Ok(await _localizationService.Translate(UserId, "reading-list-updated"));
        }

        return BadRequest(await _localizationService.Translate(UserId, "reading-list-item-delete"));
    }

    /// <summary>
    /// Deletes a reading list
    /// </summary>
    /// <param name="readingListId"></param>
    /// <returns></returns>
    [HttpDelete]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> DeleteList([FromQuery] int readingListId)
    {
        var user = await _readingListService.UserHasReadingListAccess(readingListId, Username!);
        if (user == null)
        {
            return BadRequest(await _localizationService.Translate(UserId, "reading-list-permission"));
        }

        if (await _readingListService.DeleteReadingList(readingListId, user))
            return Ok(await _localizationService.Translate(UserId, "reading-list-deleted"));

        return BadRequest(await _localizationService.Translate(UserId, "generic-reading-list-delete"));
    }

    /// <summary>
    /// Creates a new List with a unique title. Returns the new ReadingList back
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("create")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<ReadingListDto>> CreateList(CreateReadingListDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(Username!, AppUserIncludes.ReadingLists);
        if (user == null) return Unauthorized();

        try
        {
            await _readingListService.CreateReadingListForUser(user, dto.Title);
        }
        catch (KavitaException ex)
        {
            return BadRequest(await _localizationService.Translate(UserId, ex.Message));
        }

        return Ok(await _unitOfWork.ReadingListRepository.GetReadingListDtoByTitleAsync(user.Id, dto.Title));
    }

    /// <summary>
    /// Update the properties (title, summary) of a reading list
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> UpdateList(UpdateReadingListDto dto)
    {
        var readingList = await _unitOfWork.ReadingListRepository.GetReadingListByIdAsync(dto.ReadingListId);
        if (readingList == null) return BadRequest(await _localizationService.Translate(UserId, "reading-list-doesnt-exist"));

        var user = await _readingListService.UserHasReadingListAccess(readingList.Id, Username!);
        if (user == null)
        {
            return BadRequest(await _localizationService.Translate(UserId, "reading-list-permission"));
        }

        try
        {
            await _readingListService.UpdateReadingList(readingList, dto);
        }
        catch (KavitaException ex)
        {
            return BadRequest(await _localizationService.Translate(UserId, ex.Message));
        }

        return Ok(await _localizationService.Translate(UserId, "reading-list-updated"));
    }

    /// <summary>
    /// Adds all chapters from a Series to a reading list
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update-by-series")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> UpdateListBySeries(UpdateReadingListBySeriesDto dto)
    {
        var user = await _readingListService.UserHasReadingListAccess(dto.ReadingListId, Username!);
        if (user == null)
        {
            return BadRequest(await _localizationService.Translate(UserId, "reading-list-permission"));
        }

        var readingList = user.ReadingLists.SingleOrDefault(l => l.Id == dto.ReadingListId);
        if (readingList == null) return BadRequest(await _localizationService.Translate(UserId, "reading-list-doesnt-exist"));
        var chapterIdsForSeries =
            await _unitOfWork.SeriesRepository.GetChapterIdsForSeriesAsync([dto.SeriesId]);

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
                return Ok(await _localizationService.Translate(UserId, "reading-list-updated"));
            }
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
        }

        return Ok(await _localizationService.Translate(UserId, "nothing-to-do"));
    }


    /// <summary>
    /// Adds all chapters from a list of volumes and chapters to a reading list
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update-by-multiple")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> UpdateListByMultiple(UpdateReadingListByMultipleDto dto)
    {
        var user = await _readingListService.UserHasReadingListAccess(dto.ReadingListId, Username!);
        if (user == null)
        {
            return BadRequest(await _localizationService.Translate(UserId, "reading-list-permission"));
        }
        var readingList = user.ReadingLists.SingleOrDefault(l => l.Id == dto.ReadingListId);
        if (readingList == null) return BadRequest(await _localizationService.Translate(UserId, "reading-list-doesnt-exist"));

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
                return Ok(await _localizationService.Translate(UserId, "reading-list-updated"));
            }
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
        }

        return Ok(await _localizationService.Translate(UserId, "nothing-to-do"));
    }

    /// <summary>
    /// Adds all chapters from a list of series to a reading list
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update-by-multiple-series")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> UpdateListByMultipleSeries(UpdateReadingListByMultipleSeriesDto dto)
    {
        var user = await _readingListService.UserHasReadingListAccess(dto.ReadingListId, Username!);
        if (user == null)
        {
            return BadRequest(await _localizationService.Translate(UserId, "reading-list-permission"));
        }
        var readingList = user.ReadingLists.SingleOrDefault(l => l.Id == dto.ReadingListId);
        if (readingList == null) return BadRequest(await _localizationService.Translate(UserId, "reading-list-doesnt-exist"));

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
                return Ok(await _localizationService.Translate(UserId, "reading-list-updated"));
            }
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
        }

        return Ok(await _localizationService.Translate(UserId, "nothing-to-do"));
    }

    [HttpPost("update-by-volume")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> UpdateListByVolume(UpdateReadingListByVolumeDto dto)
    {
        var user = await _readingListService.UserHasReadingListAccess(dto.ReadingListId, Username!);
        if (user == null)
        {
            return BadRequest(await _localizationService.Translate(UserId, "reading-list-permission"));
        }
        var readingList = user.ReadingLists.SingleOrDefault(l => l.Id == dto.ReadingListId);
        if (readingList == null) return BadRequest(await _localizationService.Translate(UserId, "reading-list-doesnt-exist"));

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
                return Ok(await _localizationService.Translate(UserId, "reading-list-updated"));
            }
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
        }

        return Ok(await _localizationService.Translate(UserId, "nothing-to-do"));
    }

    [HttpPost("update-by-chapter")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> UpdateListByChapter(UpdateReadingListByChapterDto dto)
    {
        var user = await _readingListService.UserHasReadingListAccess(dto.ReadingListId, Username!);
        if (user == null)
        {
            return BadRequest(await _localizationService.Translate(UserId, "reading-list-permission"));
        }
        var readingList = user.ReadingLists.SingleOrDefault(l => l.Id == dto.ReadingListId);
        if (readingList == null) return BadRequest(await _localizationService.Translate(UserId, "reading-list-doesnt-exist"));

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
                return Ok(await _localizationService.Translate(UserId, "reading-list-updated"));
            }
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
        }

        return Ok(await _localizationService.Translate(UserId, "nothing-to-do"));
    }


    /// <summary>
    /// Returns a list of a given role associated with the reading list
    /// </summary>
    /// <param name="readingListId"></param>
    /// <param name="role">PersonRole</param>
    /// <returns></returns>
    [HttpGet("people")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.TenMinute, VaryByQueryKeys = ["readingListId", "role"])]
    public ActionResult<IEnumerable<PersonDto>> GetPeopleByRoleForList(int readingListId, PersonRole role)
    {
        return Ok(_unitOfWork.ReadingListRepository.GetReadingListPeopleAsync(readingListId, role));
    }

    /// <summary>
    /// Returns all people in given roles for a reading list
    /// </summary>
    /// <param name="readingListId"></param>
    /// <returns></returns>
    [HttpGet("all-people")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.TenMinute, VaryByQueryKeys = ["readingListId"])]
    public async Task<ActionResult<IEnumerable<PersonDto>>> GetAllPeopleForList(int readingListId)
    {
        return Ok(await _unitOfWork.ReadingListRepository.GetReadingListAllPeopleAsync(readingListId));
    }

    /// <summary>
    /// Returns the next chapter within the reading list
    /// </summary>
    /// <param name="currentChapterId"></param>
    /// <param name="readingListId"></param>
    /// <returns>Chapter ID for next item, -1 if nothing exists</returns>
    [HttpGet("next-chapter")]
    public async Task<ActionResult<int>> GetNextChapter(int currentChapterId, int readingListId)
    {
        var items = (await _unitOfWork.ReadingListRepository.GetReadingListItemsByIdAsync(readingListId)).ToList();
        var readingListItem = items.SingleOrDefault(rl => rl.ChapterId == currentChapterId);
        if (readingListItem == null) return BadRequest(await _localizationService.Translate(UserId, "chapter-doesnt-exist"));
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
    /// <returns>ChapterId for next item, -1 if nothing exists</returns>
    [HttpGet("prev-chapter")]
    public async Task<ActionResult<int>> GetPrevChapter(int currentChapterId, int readingListId)
    {
        var items = (await _unitOfWork.ReadingListRepository.GetReadingListItemsByIdAsync(readingListId)).ToList();
        var readingListItem = items.SingleOrDefault(rl => rl.ChapterId == currentChapterId);
        if (readingListItem == null) return BadRequest(await _localizationService.Translate(UserId, "chapter-doesnt-exist"));
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
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
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
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> PromoteMultipleReadingLists(PromoteReadingListsDto dto)
    {
        // This needs to take into account owner as I can select other users cards
        var userId = UserId;
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
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(UserId, AppUserIncludes.ReadingLists);
        if (user == null) return Unauthorized();

        user.ReadingLists = user.ReadingLists.Where(uc => !dto.ReadingListIds.Contains(uc.Id)).ToList();
        _unitOfWork.UserRepository.Update(user);


        if (!_unitOfWork.HasChanges()) return Ok();
        await _unitOfWork.CommitAsync();

        return Ok();
    }

    /// <summary>
    /// Returns random information about a Reading List
    /// </summary>
    /// <param name="readingListId"></param>
    /// <returns></returns>
    [HttpGet("info")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Hour, VaryByQueryKeys = ["readingListId"])]
    public async Task<ActionResult<ReadingListInfoDto?>> GetReadingListInfo(int readingListId)
    {
        var result = await _unitOfWork.ReadingListRepository.GetReadingListInfoAsync(readingListId);

        if (result == null) return Ok(null);

        var timeEstimate = ReaderService.GetTimeEstimate(result.WordCount, result.Pages, result.IsAllEpub);

        result.MinHoursToRead = timeEstimate.MinHours;
        result.AvgHoursToRead = timeEstimate.AvgHours;
        result.MaxHoursToRead = timeEstimate.MaxHours;

        return Ok(result);
    }
}
