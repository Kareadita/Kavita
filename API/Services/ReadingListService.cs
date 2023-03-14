﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using API.Comparators;
using API.Data;
using API.Data.Repositories;
using API.DTOs.ReadingLists;
using API.DTOs.ReadingLists.CBL;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using API.SignalR;
using Kavita.Common;
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
    Task CalculateStartAndEndDates(ReadingList readingListWithItems);
    Task<string> GenerateMergedImage(int readingListId);
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


        if (NumberHelper.IsValidMonth(dto.StartingMonth))
        {
            readingList.StartingMonth = dto.StartingMonth;
        }
        if (NumberHelper.IsValidYear(dto.StartingYear))
        {
            readingList.StartingYear = dto.StartingYear;
        }
        if (NumberHelper.IsValidMonth(dto.EndingMonth))
        {
            readingList.EndingMonth = dto.EndingMonth;
        }
        if (NumberHelper.IsValidYear(dto.EndingYear))
        {
            readingList.EndingYear = dto.EndingYear;
        }


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
            await CalculateStartAndEndDates(readingList);

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
        await CalculateStartAndEndDates(readingList);

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
    /// Calculates the Start month/year and Ending month/year
    /// </summary>
    /// <param name="readingListWithItems">Reading list should have all items</param>
    public async Task CalculateStartAndEndDates(ReadingList readingListWithItems)
    {
        var items = readingListWithItems.Items;
        if (readingListWithItems.Items.All(i => i.Chapter == null))
        {
            items =
                (await _unitOfWork.ReadingListRepository.GetReadingListByIdAsync(readingListWithItems.Id, ReadingListIncludes.ItemChapter))?.Items;
        }
        if (items == null || items.Count == 0) return;

        if (items.First().Chapter == null)
        {
            _logger.LogError("Tried to calculate release dates for Reading List, but missing Chapter entities");
            return;
        }
        var maxReleaseDate = items.Max(item => item.Chapter.ReleaseDate);
        var minReleaseDate = items.Min(item => item.Chapter.ReleaseDate);
        if (maxReleaseDate != DateTime.MinValue)
        {
            readingListWithItems.EndingMonth = maxReleaseDate.Month;
            readingListWithItems.EndingYear = maxReleaseDate.Year;
        }
        if (minReleaseDate != DateTime.MinValue)
        {
            readingListWithItems.StartingMonth = minReleaseDate.Month;
            readingListWithItems.StartingYear = minReleaseDate.Year;
        }
    }

    public Task<string?> GenerateMergedImage(int readingListId)
    {
        throw new NotImplementedException();
        // var coverImages = (await _unitOfWork.ReadingListRepository.GetFirstFourCoverImagesByReadingListId(readingListId)).ToList();
        // if (coverImages.Count < 4) return null;
        // var fullImages = coverImages
        //     .Select(c => _directoryService.FileSystem.Path.Join(_directoryService.CoverImageDirectory, c)).ToList();
        //
        // var combinedFile = ImageService.CreateMergedImage(fullImages, _directoryService.FileSystem.Path.Join(_directoryService.TempDirectory, $"{readingListId}.png"));
        // // webp needs to be handled
        // return combinedFile;
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
        if (user == null || !await UserHasReadingListAccess(readingListId, user))
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
            SuccessfulInserts = new List<CblBookResult>()
        };
        if (IsCblEmpty(cblReading, importSummary, out var readingListFromCbl)) return readingListFromCbl;

        // Is there another reading list with the same name?
        if (await _unitOfWork.ReadingListRepository.ReadingListExists(cblReading.Name))
        {
            importSummary.Success = CblImportResult.Fail;
            importSummary.Results.Add(new CblBookResult()
            {
                Reason = CblImportReason.NameConflict,
                ReadingListName = cblReading.Name
            });
        }

        var uniqueSeries = cblReading.Books.Book.Select(b => Tasks.Scanner.Parser.Parser.Normalize(b.Series)).Distinct().ToList();
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
        foreach (var conflict in conflicts)
        {
            importSummary.Results.Add(new CblBookResult()
            {
                Reason = CblImportReason.SeriesCollision,
                Series = conflict.Name,
                LibraryId = conflict.LibraryId,
                SeriesId = conflict.Id,
            });
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
        _logger.LogDebug("Importing {ReadingListName} CBL for User {UserName}", cblReading.Name, user!.UserName);
        var importSummary = new CblImportSummaryDto()
        {
            CblName = cblReading.Name,
            Success = CblImportResult.Success,
            Results = new List<CblBookResult>(),
            SuccessfulInserts = new List<CblBookResult>()
        };

        var uniqueSeries = cblReading.Books.Book.Select(b => Tasks.Scanner.Parser.Parser.Normalize(b.Series)).Distinct().ToList();
        var userSeries =
            (await _unitOfWork.SeriesRepository.GetAllSeriesByNameAsync(uniqueSeries, userId, SeriesIncludes.Chapters)).ToList();
        var allSeries = userSeries.ToDictionary(s => Tasks.Scanner.Parser.Parser.Normalize(s.Name));
        var allSeriesLocalized = userSeries.ToDictionary(s => Tasks.Scanner.Parser.Parser.Normalize(s.LocalizedName));

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
            if (!allSeries.TryGetValue(normalizedSeries, out var bookSeries) && !allSeriesLocalized.TryGetValue(normalizedSeries, out bookSeries))
            {
                importSummary.Results.Add(new CblBookResult(book)
                {
                    Reason = CblImportReason.SeriesMissing,
                    Order = i
                });
                continue;
            }
            // Prioritize lookup by Volume then Chapter, but allow fallback to just Chapter
            var bookVolume = string.IsNullOrEmpty(book.Volume)
                ? Tasks.Scanner.Parser.Parser.DefaultVolume
                : book.Volume;
            var matchingVolume = bookSeries.Volumes.FirstOrDefault(v => bookVolume == v.Name) ?? bookSeries.Volumes.FirstOrDefault(v => v.Number == 0);
            if (matchingVolume == null)
            {
                importSummary.Results.Add(new CblBookResult(book)
                {
                    Reason = CblImportReason.VolumeMissing,
                    LibraryId = bookSeries.LibraryId,
                    Order = i
                });
                continue;
            }

            // We need to handle chapter 0 or empty string when it's just a volume
            var bookNumber = string.IsNullOrEmpty(book.Number)
                ? Tasks.Scanner.Parser.Parser.DefaultChapter
                : book.Number;
            var chapter = matchingVolume.Chapters.FirstOrDefault(c => c.Number == bookNumber);
            if (chapter == null)
            {
                importSummary.Results.Add(new CblBookResult(book)
                {
                    Reason = CblImportReason.ChapterMissing,
                    LibraryId = bookSeries.LibraryId,
                    Order = i
                });
                continue;
            }

            // See if a matching item already exists
            ExistsOrAddReadingListItem(readingList, bookSeries.Id, matchingVolume.Id, chapter.Id);
            importSummary.SuccessfulInserts.Add(new CblBookResult(book)
            {
                Reason = CblImportReason.Success,
                Order = i
            });
        }

        if (importSummary.SuccessfulInserts.Count != cblReading.Books.Book.Count || importSummary.Results.Count > 0)
        {
            importSummary.Success = CblImportResult.Partial;
        }

        if (importSummary.SuccessfulInserts.Count == 0 && importSummary.Results.Count == cblReading.Books.Book.Count)
        {
            importSummary.Success = CblImportResult.Fail;
        }

        if (dryRun) return importSummary;

        await CalculateReadingListAgeRating(readingList);
        await CalculateStartAndEndDates(readingList);

        // For CBL Import only we override pre-calculated dates
        if (NumberHelper.IsValidMonth(cblReading.StartMonth)) readingList.StartingMonth = cblReading.StartMonth;
        if (NumberHelper.IsValidYear(cblReading.StartYear)) readingList.StartingYear = cblReading.StartYear;
        if (NumberHelper.IsValidMonth(cblReading.EndMonth)) readingList.EndingMonth = cblReading.EndMonth;
        if (NumberHelper.IsValidYear(cblReading.EndYear)) readingList.EndingYear = cblReading.EndYear;

        if (!string.IsNullOrEmpty(readingList.Summary?.Trim()))
        {
            readingList.Summary = readingList.Summary?.Trim();
        }

        // If there are no items, don't create a blank list
        if (!_unitOfWork.HasChanges() || !readingList.Items.Any()) return importSummary;
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
