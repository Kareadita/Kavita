using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using API.Comparators;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.ReadingLists;
using API.DTOs.ReadingLists.CBL;
using API.Entities;
using API.Entities.Enums;
using API.SignalR;
using Kavita.Common;
using API.Entities.Enums;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;

namespace API.Services;

public interface IReadingListService
{
    Task<ReadingList> CreateReadingListForUser(AppUser userWithReadingList, string title);
    Task UpdateReadingList(ReadingList readingList, UpdateReadingListDto dto);
    Task<bool> RemoveFullyReadItems(int readingListId, AppUser user);
    Task<bool> UpdateReadingListItemPosition(UpdateReadingListPosition dto);
    Task<bool> DeleteReadingListItem(UpdateReadingListPosition dto);
    Task<AppUser?> UserHasReadingListAccess(int readingListId, string username);
    Task<bool> DeleteReadingList(int readingListId, AppUser user);
    Task CalculateReadingListAgeRating(ReadingList readingList);
    Task<bool> AddChaptersToReadingList(int seriesId, IList<int> chapterIds,
        ReadingList readingList);

    Task<CblImportSummaryDto> ValidateCblFile(int userId, CblReadingList cblReading);
    Task<CblImportSummaryDto> CreateReadingListFromCbl(int userId, CblReadingList cblReading, bool dryRun = false);
}

/// <summary>
/// Methods responsible for management of Reading Lists
/// </summary>
/// <remarks>If called from API layer, expected for <see cref="UserHasReadingListAccess"/> to be called beforehand</remarks>
public class ReadingListService : IReadingListService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReadingListService> _logger;
    private readonly IEventHub _eventHub;
    private readonly ChapterSortComparerZeroFirst _chapterSortComparerForInChapterSorting = ChapterSortComparerZeroFirst.Default;
    private static readonly Regex JustNumbers = new Regex(@"^\d+$", RegexOptions.Compiled | RegexOptions.IgnoreCase,
        Tasks.Scanner.Parser.Parser.RegexTimeout);

    public ReadingListService(IUnitOfWork unitOfWork, ILogger<ReadingListService> logger, IEventHub eventHub)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _eventHub = eventHub;
    }

    public static string FormatTitle(ReadingListItemDto item)
    {
        var title = string.Empty;
        if (item.ChapterNumber == Tasks.Scanner.Parser.Parser.DefaultChapter && item.VolumeNumber != Tasks.Scanner.Parser.Parser.DefaultVolume) {
            title = $"Volume {item.VolumeNumber}";
        }

        if (item.SeriesFormat == MangaFormat.Epub) {
            var specialTitle = Tasks.Scanner.Parser.Parser.CleanSpecialTitle(item.ChapterNumber);
            if (specialTitle == Tasks.Scanner.Parser.Parser.DefaultChapter)
            {
                if (!string.IsNullOrEmpty(item.ChapterTitleName))
                {
                    title = item.ChapterTitleName;
                }
                else
                {
                    title = $"Volume {Tasks.Scanner.Parser.Parser.CleanSpecialTitle(item.VolumeNumber)}";
                }
            } else {
                title = $"Volume {specialTitle}";
            }
        }

        var chapterNum = item.ChapterNumber;
        if (!string.IsNullOrEmpty(chapterNum) && !JustNumbers.Match(item.ChapterNumber).Success) {
            chapterNum = Tasks.Scanner.Parser.Parser.CleanSpecialTitle(item.ChapterNumber);
        }

        if (title != string.Empty) return title;

        if (item.ChapterNumber == Tasks.Scanner.Parser.Parser.DefaultChapter &&
            !string.IsNullOrEmpty(item.ChapterTitleName))
        {
            title = item.ChapterTitleName;
        }
        else
        {
            title = ReaderService.FormatChapterName(item.LibraryType, true, true) + chapterNum;
        }
        return title;
    }


    /// <summary>
    /// Creates a new Reading List for a User
    /// </summary>
    /// <param name="userWithReadingList"></param>
    /// <param name="title"></param>
    /// <returns></returns>
    /// <exception cref="KavitaException"></exception>
    public async Task<ReadingList> CreateReadingListForUser(AppUser userWithReadingList, string title)
    {
        // When creating, we need to make sure Title is unique
        // TODO: Perform normalization
        var hasExisting = userWithReadingList.ReadingLists.Any(l => l.Title.Equals(title));
        if (hasExisting)
        {
            throw new KavitaException("A list of this name already exists");
        }

        var readingList = DbFactory.ReadingList(title, string.Empty, false);
        userWithReadingList.ReadingLists.Add(readingList);

        if (!_unitOfWork.HasChanges()) throw new KavitaException("There was a problem creating list");
        await _unitOfWork.CommitAsync();
        return readingList;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="readingList"></param>
    /// <param name="dto"></param>
    /// <exception cref="KavitaException"></exception>
    public async Task UpdateReadingList(ReadingList readingList, UpdateReadingListDto dto)
    {
        dto.Title = dto.Title.Trim();
        if (string.IsNullOrEmpty(dto.Title)) throw new KavitaException("Title must be set");

        if (!dto.Title.Equals(readingList.Title) && await _unitOfWork.ReadingListRepository.ReadingListExists(dto.Title))
            throw new KavitaException("Reading list already exists");

        readingList.Summary = dto.Summary;
        readingList.Title = dto.Title.Trim();
        readingList.NormalizedTitle = Tasks.Scanner.Parser.Parser.Normalize(readingList.Title);
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

        if (!_unitOfWork.HasChanges()) return;
        await _unitOfWork.CommitAsync();
    }

    /// <summary>
    /// Removes all entries that are fully read from the reading list. This commits
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

            var readingList = await _unitOfWork.ReadingListRepository.GetReadingListByIdAsync(readingListId);
            if (readingList == null) return true;
            await CalculateReadingListAgeRating(readingList);

            if (!_unitOfWork.HasChanges()) return true;

            return await _unitOfWork.CommitAsync();
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
        if (item != null)
        {
            items.Remove(item);
            items.Insert(dto.ToPosition, item);
        }

        for (var i = 0; i < items.Count; i++)
        {
            items[i].Order = i;
        }

        if (!_unitOfWork.HasChanges()) return true;

        return await _unitOfWork.CommitAsync();
    }

    /// <summary>
    /// Removes a certain reading list item from a reading list
    /// </summary>
    /// <param name="dto">Only ReadingListId and ReadingListItemId are used</param>
    /// <returns></returns>
    public async Task<bool> DeleteReadingListItem(UpdateReadingListPosition dto)
    {
        var readingList = await _unitOfWork.ReadingListRepository.GetReadingListByIdAsync(dto.ReadingListId);
        if (readingList == null) return false;
        readingList.Items = readingList.Items.Where(r => r.Id != dto.ReadingListItemId).OrderBy(r => r.Order).ToList();

        var index = 0;
        foreach (var readingListItem in readingList.Items)
        {
            readingListItem.Order = index;
            index++;
        }

        await CalculateReadingListAgeRating(readingList);

        if (!_unitOfWork.HasChanges()) return true;

        return await _unitOfWork.CommitAsync();
    }

    /// <summary>
    /// Calculates the highest Age Rating from each Reading List Item
    /// </summary>
    /// <param name="readingList"></param>
    public async Task CalculateReadingListAgeRating(ReadingList readingList)
    {
        await CalculateReadingListAgeRating(readingList, readingList.Items.Select(i => i.SeriesId));
    }

    /// <summary>
    /// Calculates the highest Age Rating from each Reading List Item
    /// </summary>
    /// <remarks>This method is used when the ReadingList doesn't have items yet</remarks>
    /// <param name="readingList"></param>
    /// <param name="seriesIds">The series ids of all the reading list items</param>
    private async Task CalculateReadingListAgeRating(ReadingList readingList, IEnumerable<int> seriesIds)
    {
        var ageRating = await _unitOfWork.SeriesRepository.GetMaxAgeRatingFromSeriesAsync(seriesIds);
        if (ageRating == null) readingList.AgeRating = AgeRating.Unknown;
        else readingList.AgeRating = (AgeRating) ageRating;
    }

    /// <summary>
    /// Validates the user has access to the reading list to perform actions on it
    /// </summary>
    /// <param name="readingListId"></param>
    /// <param name="username"></param>
    /// <returns></returns>
    public async Task<AppUser?> UserHasReadingListAccess(int readingListId, string username)
    {
        // We need full reading list with items as this is used by many areas that manipulate items
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(username,
            AppUserIncludes.ReadingListsWithItems);
        if (!await UserHasReadingListAccess(readingListId, user))
        {
            return null;
        }

        return user;
    }

    /// <summary>
    /// User must have ReadingList on it
    /// </summary>
    /// <param name="readingListId"></param>
    /// <param name="user"></param>
    /// <returns></returns>
    private async Task<bool> UserHasReadingListAccess(int readingListId, AppUser user)
    {
        return user.ReadingLists.Any(rl => rl.Id == readingListId) || await _unitOfWork.UserRepository.IsUserAdminAsync(user);
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
        if (readingList == null) return true;
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
            lastOrder = readingList.Items.DefaultIfEmpty().Max(rli => rli!.Order);
        }

        var existingChapterExists = readingList.Items.Select(rli => rli.ChapterId).ToHashSet();
        var chaptersForSeries = (await _unitOfWork.ChapterRepository.GetChaptersByIdsAsync(chapterIds, ChapterIncludes.Volumes))
            .OrderBy(c => Tasks.Scanner.Parser.Parser.MinNumberFromRange(c.Volume.Name))
            .ThenBy(x => double.Parse(x.Number), _chapterSortComparerForInChapterSorting)
            .ToList();

        var index = readingList.Items.Count == 0 ? 0 : lastOrder + 1;
        foreach (var chapter in chaptersForSeries.Where(chapter => !existingChapterExists.Contains(chapter.Id)))
        {
            readingList.Items.Add(DbFactory.ReadingListItem(index, seriesId, chapter.VolumeId, chapter.Id));
            index += 1;
        }

        await CalculateReadingListAgeRating(readingList, new []{ seriesId });

        return index > lastOrder + 1;
    }

    /// <summary>
    /// Check for File issues like: No entries, Reading List Name collision, Duplicate Series across Libraries
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="cblReading"></param>
    public async Task<CblImportSummaryDto> ValidateCblFile(int userId, CblReadingList cblReading)
    {
        var importSummary = new CblImportSummaryDto()
        {
            CblName = cblReading.Name,
            Success = CblImportResult.Success,
            Results = new List<CblBookResult>(),
            SuccessfulInserts = new List<CblBookResult>(),
            Conflicts = new List<SeriesDto>(),
            Conflicts2 = new List<CblConflictQuestion>()
        };
        if (IsCblEmpty(cblReading, importSummary, out var readingListFromCbl)) return readingListFromCbl;

        var uniqueSeries = cblReading.Books.Book.Select(b => Tasks.Scanner.Parser.Parser.Normalize(b.Series)).Distinct();
        var userSeries =
            (await _unitOfWork.SeriesRepository.GetAllSeriesByNameAsync(uniqueSeries, userId, SeriesIncludes.Chapters)).ToList();
        if (!userSeries.Any())
        {
            // Report that no series exist in the reading list
            importSummary.Results.Add(new CblBookResult()
            {
                Reason = CblImportReason.AllSeriesMissing
            });
            importSummary.Success = CblImportResult.Fail;
            return importSummary;
        }

        var conflicts = FindCblImportConflicts(userSeries);
        if (!conflicts.Any()) return importSummary;

        importSummary.Success = CblImportResult.Fail;
        if (conflicts.Count == cblReading.Books.Book.Count)
        {
            importSummary.Results.Add(new CblBookResult()
            {
                Reason = CblImportReason.AllChapterMissing,
            });
        }
        else
        {
            foreach (var conflict in conflicts)
            {
                importSummary.Results.Add(new CblBookResult()
                {
                    Reason = CblImportReason.SeriesCollision,
                    Series = conflict.Name
                });
            }
        }

        return importSummary;
    }


    /// <summary>
    /// Imports (or pretends to) a cbl into a reading list. Call <see cref="ValidateCblFile"/> first!
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="cblReading"></param>
    /// <param name="dryRun"></param>
    /// <returns></returns>
    public async Task<CblImportSummaryDto> CreateReadingListFromCbl(int userId, CblReadingList cblReading, bool dryRun = false)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.ReadingListsWithItems);
        _logger.LogDebug("Importing {ReadingListName} CBL for User {UserName}", cblReading.Name, user.UserName);
        var importSummary = new CblImportSummaryDto()
        {
            CblName = cblReading.Name,
            Success = CblImportResult.Success,
            Results = new List<CblBookResult>(),
            SuccessfulInserts = new List<CblBookResult>()
        };

        var uniqueSeries = cblReading.Books.Book.Select(b => Tasks.Scanner.Parser.Parser.Normalize(b.Series)).Distinct();
        var userSeries =
            (await _unitOfWork.SeriesRepository.GetAllSeriesByNameAsync(uniqueSeries, userId, SeriesIncludes.Chapters)).ToList();
        var allSeries = userSeries.ToDictionary(s => Tasks.Scanner.Parser.Parser.Normalize(s.Name));

        var readingListNameNormalized = Tasks.Scanner.Parser.Parser.Normalize(cblReading.Name);
        // Get all the user's reading lists
        var allReadingLists = (user.ReadingLists).ToDictionary(s => s.NormalizedTitle);
        if (!allReadingLists.TryGetValue(readingListNameNormalized, out var readingList))
        {
            readingList = DbFactory.ReadingList(cblReading.Name, string.Empty, false);
            user.ReadingLists.Add(readingList);
        }
        else
        {
            // Reading List exists, check if we own it
            if (user.ReadingLists.All(l => l.NormalizedTitle != readingListNameNormalized))
            {
                importSummary.Results.Add(new CblBookResult()
                {
                    Reason = CblImportReason.NameConflict
                });
                importSummary.Success = CblImportResult.Fail;
                return importSummary;
            }
        }

        readingList.Items ??= new List<ReadingListItem>();
        foreach (var (book, i) in cblReading.Books.Book.Select((value, i) => ( value, i )))
        {
            var normalizedSeries = Tasks.Scanner.Parser.Parser.Normalize(book.Series);
            if (!allSeries.TryGetValue(normalizedSeries, out var bookSeries))
            {
                importSummary.Results.Add(new CblBookResult(book)
                {
                    Reason = CblImportReason.SeriesMissing
                });
                continue;
            }
            // Prioritize lookup by Volume then Chapter, but allow fallback to just Chapter
            var matchingVolume = bookSeries.Volumes.FirstOrDefault(v => book.Volume == v.Name) ?? bookSeries.Volumes.FirstOrDefault(v => v.Number == 0);
            if (matchingVolume == null)
            {
                importSummary.Results.Add(new CblBookResult(book)
                {
                    Reason = CblImportReason.VolumeMissing
                });
                continue;
            }

            var chapter = matchingVolume.Chapters.FirstOrDefault(c => c.Number == book.Number);
            if (chapter == null)
            {
                importSummary.Results.Add(new CblBookResult(book)
                {
                    Reason = CblImportReason.ChapterMissing
                });
                continue;
            }

            // See if a matching item already exists
            ExistsOrAddReadingListItem(readingList, bookSeries.Id, matchingVolume.Id, chapter.Id);
            importSummary.SuccessfulInserts.Add(new CblBookResult(book));
        }

        if (importSummary.SuccessfulInserts.Count != cblReading.Books.Book.Count || importSummary.Results.Count > 0)
        {
            importSummary.Success = CblImportResult.Partial;
        }

        await CalculateReadingListAgeRating(readingList);

        if (!dryRun) return importSummary;

        if (!_unitOfWork.HasChanges()) return importSummary;
        await _unitOfWork.CommitAsync();


        return importSummary;
    }

    private static IList<Series> FindCblImportConflicts(IEnumerable<Series> userSeries)
    {
        var dict = new HashSet<string>();
        return userSeries.Where(series => !dict.Add(Tasks.Scanner.Parser.Parser.Normalize(series.Name))).ToList();
    }

    private static bool IsCblEmpty(CblReadingList cblReading, CblImportSummaryDto importSummary,
        out CblImportSummaryDto readingListFromCbl)
    {
        readingListFromCbl = new CblImportSummaryDto();
        if (cblReading.Books == null || cblReading.Books.Book.Count == 0)
        {
            importSummary.Results.Add(new CblBookResult()
            {
                Reason = CblImportReason.EmptyFile
            });
            importSummary.Success = CblImportResult.Fail;
            readingListFromCbl = importSummary;
            return true;
        }

        return false;
    }

    private static void ExistsOrAddReadingListItem(ReadingList readingList, int seriesId, int volumeId, int chapterId)
    {
        var readingListItem =
            readingList.Items.FirstOrDefault(item =>
                item.SeriesId == seriesId && item.ChapterId == chapterId);
        if (readingListItem != null) return;

        readingListItem = DbFactory.ReadingListItem(readingList.Items.Count, seriesId,
            volumeId, chapterId);
        readingList.Items.Add(readingListItem);
    }

    public static CblReadingList LoadCblFromPath(string path)
    {
        var reader = new System.Xml.Serialization.XmlSerializer(typeof(CblReadingList));
        using var file = new StreamReader(path);
        var cblReadingList = (CblReadingList) reader.Deserialize(file);
        file.Close();
        return cblReadingList;
    }
}
