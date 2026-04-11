using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs.Person;
using Kavita.Models.DTOs.ReadingLists;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.ReadingLists;

namespace Kavita.API.Repositories;

[Flags]
public enum ReadingListIncludes
{
    None = 1 << 0,
    Items = 1 << 1,
    ItemChapter = 1 << 2,
    Tags = 1 << 3,
}

public interface IReadingListRepository
{
    void Remove(ReadingListItem item);
    void Add(ReadingList list);
    void BulkRemove(IEnumerable<ReadingListItem> items);
    void Update(ReadingList list);

    Task<PagedList<ReadingListDto>> GetReadingListDtosForUserAsync(int userId, bool includePromoted, UserParams userParams, bool sortByLastModified = true, CancellationToken ct = default);
    Task<ReadingList?> GetReadingListByIdAsync(int readingListId, ReadingListIncludes includes = ReadingListIncludes.None, CancellationToken ct = default);
    Task<IList<ReadingListItemDto>> GetReadingListItemDtosByIdAsync(int readingListId, int userId, UserParams? userParams = null, CancellationToken ct = default);
    Task<ReadingListDto?> GetReadingListDtoByIdAsync(int readingListId, int userId, CancellationToken ct = default);
    Task<ReadingListDto?> GetReadingListDtoByTitleAsync(int userId, string title, CancellationToken ct = default);
    Task<IEnumerable<ReadingListItem>> GetReadingListItemsByIdAsync(int readingListId, CancellationToken ct = default);
    Task<IEnumerable<ReadingListDto>> GetReadingListDtosForSeriesAndUserAsync(int userId, int seriesId,
        bool includePromoted, CancellationToken ct = default);
    Task<IEnumerable<ReadingListDto>> GetReadingListDtosForChapterAndUserAsync(int userId, int chapterId,
        bool includePromoted, CancellationToken ct = default);
    Task<int> Count(CancellationToken ct = default);
    Task<string?> GetCoverImageAsync(int readingListId, CancellationToken ct = default);
    Task<IList<string>> GetRandomCoverImagesAsync(int readingListId, CancellationToken ct = default);
    Task<IList<string>> GetAllCoverImagesAsync(CancellationToken ct = default);
    Task<bool> ReadingListExists(string name, int? readingListId = null, CancellationToken ct = default);
    Task<bool> ReadingListExistsForUser(string name, int userId, CancellationToken ct = default);
    IEnumerable<PersonDto> GetReadingListPeopleAsync(int readingListId, PersonRole role, CancellationToken ct = default);
    Task<ReadingListCast> GetReadingListAllPeopleAsync(int readingListId, CancellationToken ct = default);
    Task<IList<ReadingList>> GetAllWithCoversInDifferentEncoding(EncodeFormat encodeFormat, CancellationToken ct = default);
    Task<int> RemoveReadingListsWithoutSeries(CancellationToken ct = default);
    Task<ReadingList?> GetReadingListByTitleAsync(string name, int userId, ReadingListIncludes includes = ReadingListIncludes.Items, CancellationToken ct = default);
    Task<IEnumerable<ReadingList>> GetReadingListsByIds(IList<int> ids, ReadingListIncludes includes = ReadingListIncludes.Items, CancellationToken ct = default);
    Task<IEnumerable<ReadingList>> GetReadingListsBySeriesId(int seriesId, ReadingListIncludes includes = ReadingListIncludes.Items, CancellationToken ct = default);
    Task<ReadingListInfoDto?> GetReadingListInfoAsync(int readingListId, CancellationToken ct = default);
    Task<bool> AnyUserReadingProgressAsync(int readingListId, int userId, CancellationToken ct = default);
    Task<ReadingListItemDto?> GetContinueReadingPoint(int readingListId, int userId, CancellationToken ct = default);
    Task<int> GetReadingListItemCountAsync(int readingListId, int userId, CancellationToken ct = default);
    Task<long> GetFilesizeAsync(int readingListId, int userId, CancellationToken ct = default);
    Task<Dictionary<int, long>> GetFilesizesAsync(IList<int> readingListIds, int userId, CancellationToken ct = default);
    /// <summary>
    /// Returns a map of UserId to ReadingListIds for all syncable reading lists that haven't been checked since the given threshold.
    /// </summary>
    Task<Dictionary<int, List<int>>> GetSyncableReadingListsAsync(DateTime lastCheckThreshold, CancellationToken ct = default);

    Task<List<ReadingListTagDto>> GetAllReadingListTagDtosAsync(int userId, CancellationToken ct = default);
}
