using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.Data;
using API.Data.Repositories;
using API.DTOs.ReadingLists;
using API.Entities;
using Microsoft.Extensions.Logging;

namespace API.Services;

public interface IReadingListService
{
    Task<bool> RemoveFullyReadItems(int readingListId, AppUser user);
    Task<bool> UpdateReadingListItemPosition(UpdateReadingListPosition dto);
    Task<bool> DeleteReadingListItem(UpdateReadingListPosition dto);
    Task<AppUser?> UserHasReadingListAccess(int readingListId, string username);
    Task<bool> DeleteReadingList(int readingListId, AppUser user);

    Task<bool> AddChaptersToReadingList(int seriesId, IList<int> chapterIds,
        ReadingList readingList);
}

/// <summary>
/// Methods responsible for management of Reading Lists
/// </summary>
/// <remarks>If called from API layer, expected for <see cref="UserHasReadingListAccess"/> to be called beforehand</remarks>
public class ReadingListService : IReadingListService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReadingListService> _logger;
    private readonly ChapterSortComparerZeroFirst _chapterSortComparerForInChapterSorting = new ChapterSortComparerZeroFirst();

    public ReadingListService(IUnitOfWork unitOfWork, ILogger<ReadingListService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }



    /// <summary>
    /// Removes all entries that are fully read from the reading list
    /// </summary>
    /// <remarks>If called from API layer, expected for <see cref="UserHasReadingListAccess"/> to be called beforehand</remarks>
    /// <param name="readingListId">Reading List Id</param>
    /// <param name="user">User</param>
    /// <returns></returns>
    public async Task<bool> RemoveFullyReadItems(int readingListId, AppUser user)
    {
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

            if (!_unitOfWork.HasChanges()) return true;

            await _unitOfWork.CommitAsync();
            return true;
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
        }

        return false;
    }

    /// <summary>
    /// Updates a reading list item from one position to another. This will cause items at that position to be pushed one index.
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    public async Task<bool> UpdateReadingListItemPosition(UpdateReadingListPosition dto)
    {
        var items = (await _unitOfWork.ReadingListRepository.GetReadingListItemsByIdAsync(dto.ReadingListId)).ToList();
        var item = items.Find(r => r.Id == dto.ReadingListItemId);
        items.Remove(item);
        items.Insert(dto.ToPosition, item);

        for (var i = 0; i < items.Count; i++)
        {
            items[i].Order = i;
        }

        if (!_unitOfWork.HasChanges()) return true;

        return await _unitOfWork.CommitAsync();
    }

    public async Task<bool> DeleteReadingListItem(UpdateReadingListPosition dto)
    {
        var readingList = await _unitOfWork.ReadingListRepository.GetReadingListByIdAsync(dto.ReadingListId);
        readingList.Items = readingList.Items.Where(r => r.Id != dto.ReadingListItemId).ToList();

        var index = 0;
        foreach (var readingListItem in readingList.Items)
        {
            readingListItem.Order = index;
            index++;
        }

        if (!_unitOfWork.HasChanges()) return true;

        return await _unitOfWork.CommitAsync();
    }

    /// <summary>
    /// Validates the user has access to the reading list to perform actions on it
    /// </summary>
    /// <param name="readingListId"></param>
    /// <param name="username"></param>
    /// <returns></returns>
    public async Task<AppUser?> UserHasReadingListAccess(int readingListId, string username)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(username,
            AppUserIncludes.ReadingListsWithItems);
        if (user.ReadingLists.SingleOrDefault(rl => rl.Id == readingListId) == null && !await _unitOfWork.UserRepository.IsUserAdminAsync(user))
        {
            return null;
        }

        return user;
    }

    /// <summary>
    /// Removes the Reading List from kavita
    /// </summary>
    /// <param name="readingListId"></param>
    /// <param name="user">User should have ReadingLists populated</param>
    /// <returns></returns>
    public async Task<bool> DeleteReadingList(int readingListId, AppUser user)
    {
        var readingList = await _unitOfWork.ReadingListRepository.GetReadingListByIdAsync(readingListId);
        user.ReadingLists.Remove(readingList);

        if (!_unitOfWork.HasChanges()) return true;

        return await _unitOfWork.CommitAsync();
    }

    /// <summary>
    /// Adds a list of Chapters as reading list items to the passed reading list.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="chapterIds"></param>
    /// <param name="readingList"></param>
    /// <returns>True if new chapters were added</returns>
    public async Task<bool> AddChaptersToReadingList(int seriesId, IList<int> chapterIds, ReadingList readingList)
    {
        readingList.Items ??= new List<ReadingListItem>();
        var lastOrder = 0;
        if (readingList.Items.Any())
        {
            lastOrder = readingList.Items.DefaultIfEmpty().Max(rli => rli.Order);
        }

        var existingChapterExists = readingList.Items.Select(rli => rli.ChapterId).ToHashSet();
        var chaptersForSeries = (await _unitOfWork.ChapterRepository.GetChaptersByIdsAsync(chapterIds))
            .OrderBy(c => Tasks.Scanner.Parser.Parser.MinNumberFromRange(c.Volume.Name))
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
}
