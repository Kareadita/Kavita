using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.Reading;
using Kavita.API.Services.SignalR;
using Kavita.Common;
using Kavita.Common.Extensions;
using Kavita.Common.Helpers;
using Kavita.Models.Builders;
using Kavita.Models.DTOs.ReadingLists;
using Kavita.Models.DTOs.ReadingLists.CBL;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;
using Kavita.Models.Extensions;
using Kavita.Models.Helpers;
using Kavita.Services.Extensions;
using Kavita.Services.Scanner;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Reading;

/// <summary>
/// Methods responsible for management of Reading Lists
/// </summary>
/// <remarks>If called from API layer, expected for <see cref="UserHasReadingListAccess(int, string)"/> to be called beforehand</remarks>
public class ReadingListService(
    IUnitOfWork unitOfWork,
    ILogger<ReadingListService> logger,
    IEventHub eventHub,
    IImageService imageService,
    IDirectoryService directoryService,
    IEntityNamingService namingService)
    : IReadingListService
{
    private static readonly Regex JustNumbers = new Regex(@"^\d+$", RegexOptions.Compiled | RegexOptions.IgnoreCase,
        Parser.RegexTimeout);

    public static string FormatTitle(ReadingListItemDto item)
    {
        var title = string.Empty;
        if (Parser.IsDefaultChapter(item.ChapterNumber) && !Parser.IsLooseLeafVolume(item.VolumeNumber)) {
            title = $"Volume {item.VolumeNumber}";
        }

        if (item.SeriesFormat == MangaFormat.Epub) {
            var specialTitle = Parser.CleanSpecialTitle(item.ChapterNumber);
            if (Parser.IsDefaultChapter(specialTitle))
            {
                if (!string.IsNullOrEmpty(item.ChapterTitleName))
                {
                    title = item.ChapterTitleName;
                }
                else
                {
                    title = $"Volume {Parser.CleanSpecialTitle(item.VolumeNumber)}";
                }
            }
            else if (item.VolumeNumber == Parser.SpecialVolume)
            {
                title = specialTitle;
            }
            else
            {
                title = $"Volume {specialTitle}";
            }
        }

        var chapterNum = item.ChapterNumber;
        if (!string.IsNullOrEmpty(chapterNum) && !JustNumbers.Match(item.ChapterNumber).Success) {
            chapterNum = Parser.CleanSpecialTitle(item.ChapterNumber);
        }

        if (title != string.Empty) return title;

        // item.ChapterNumber is Range
        if (Parser.IsDefaultChapter(item.ChapterNumber) &&
            !string.IsNullOrEmpty(item.ChapterTitleName))
        {
            title = item.ChapterTitleName;
        }
        else if (item.IsSpecial &&
                 (!string.IsNullOrEmpty(item.ChapterTitleName) || !string.IsNullOrEmpty(chapterNum)))
        {
            if (!string.IsNullOrEmpty(item.ChapterTitleName))
            {
                title = item.ChapterTitleName;
            }
            else
            {
                title = chapterNum;
            }

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
        var normalizedTitle = title.ToNormalized();
        var hasExisting = userWithReadingList.ReadingLists.Any(l => l.NormalizedTitle == normalizedTitle);
        if (hasExisting)
        {
            throw new KavitaException("reading-list-name-exists");
        }

        var readingList = new ReadingListBuilder(title).Build();
        userWithReadingList.ReadingLists.Add(readingList);

        if (!unitOfWork.HasChanges()) throw new KavitaException("generic-reading-list-create");
        await unitOfWork.CommitAsync();
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
        if (string.IsNullOrEmpty(dto.Title)) throw new KavitaException("reading-list-title-required");

        if (!dto.Title.Equals(readingList.Title) && await unitOfWork.ReadingListRepository.ReadingListExists(dto.Title, readingList.Id))
            throw new KavitaException("reading-list-name-exists");

        readingList.Summary = dto.Summary;
        readingList.Title = dto.Title.Trim();
        readingList.NormalizedTitle = Parser.Normalize(readingList.Title);
        readingList.Promoted = dto.Promoted;
        readingList.CoverImageLocked = dto.CoverImageLocked;


        if (NumberHelper.IsValidMonth(dto.StartingMonth) || dto.StartingMonth == 0)
        {
            readingList.StartingMonth = dto.StartingMonth;
        }
        if (NumberHelper.IsValidYear(dto.StartingYear) || dto.StartingYear == 0)
        {
            readingList.StartingYear = dto.StartingYear;
        }
        if (NumberHelper.IsValidMonth(dto.EndingMonth) || dto.EndingMonth == 0)
        {
            readingList.EndingMonth = dto.EndingMonth;
        }
        if (NumberHelper.IsValidYear(dto.EndingYear) || dto.EndingYear == 0)
        {
            readingList.EndingYear = dto.EndingYear;
        }


        if (!dto.CoverImageLocked)
        {
            readingList.CoverImageLocked = false;
            readingList.CoverImage = string.Empty;
            await eventHub.SendMessageAsync(MessageFactory.CoverUpdate,
                MessageFactory.CoverUpdateEvent(readingList.Id, MessageFactoryEntityTypes.ReadingList), false);
            unitOfWork.ReadingListRepository.Update(readingList);
        }

        unitOfWork.ReadingListRepository.Update(readingList);

        if (!unitOfWork.HasChanges()) return;
        await unitOfWork.CommitAsync();
    }

    /// <summary>
    /// Removes all entries that are fully read from the reading list. This commits
    /// </summary>
    /// <remarks>If called from API layer, expected for <see cref="UserHasReadingListAccess(int, string)"/> to be called beforehand</remarks>
    /// <param name="readingListId">Reading List Id</param>
    /// <param name="user">User</param>
    /// <returns></returns>
    public async Task<bool> RemoveFullyReadItems(int readingListId, AppUser user)
    {
        var items = await unitOfWork.ReadingListRepository.GetReadingListItemDtosByIdAsync(readingListId, user.Id);

        // Collect all Ids to remove
        var itemIdsToRemove = items.Where(item => item.PagesRead == item.PagesTotal).Select(item => item.Id).ToList();

        if (itemIdsToRemove.Count == 0) return true;
        try
        {
            var listItems =
                (await unitOfWork.ReadingListRepository.GetReadingListItemsByIdAsync(readingListId)).Where(r =>
                    itemIdsToRemove.Contains(r.Id));
            unitOfWork.ReadingListRepository.BulkRemove(listItems);

            var readingList = await unitOfWork.ReadingListRepository.GetReadingListByIdAsync(readingListId);
            if (readingList == null) return true;
            await CalculateReadingListAgeRating(readingList);
            await CalculateStartAndEndDates(readingList);

            if (!unitOfWork.HasChanges()) return true;
            return await unitOfWork.CommitAsync();
        }
        catch
        {
            await unitOfWork.RollbackAsync();
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
        var items = (await unitOfWork.ReadingListRepository.GetReadingListItemsByIdAsync(dto.ReadingListId)).ToList();
        OrderableHelper.ReorderItems(items, dto.ReadingListItemId, dto.ToPosition);

        if (!unitOfWork.HasChanges()) return true;

        return await unitOfWork.CommitAsync();
    }

    /// <summary>
    /// Removes a certain reading list item from a reading list
    /// </summary>
    /// <param name="dto">Only ReadingListId and ReadingListItemId are used</param>
    /// <returns></returns>
    public async Task<bool> DeleteReadingListItem(UpdateReadingListPosition dto)
    {
        var readingList = await unitOfWork.ReadingListRepository.GetReadingListByIdAsync(dto.ReadingListId);
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

        if (!unitOfWork.HasChanges()) return true;

        return await unitOfWork.CommitAsync();
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
    /// <param name="readingListWithItems">Reading list should have all items and Chapters</param>
    public async Task CalculateStartAndEndDates(ReadingList readingListWithItems)
    {
        var items = readingListWithItems.Items;
        if (readingListWithItems.Items.All(i => i.Chapter == null))
        {
            items =
                (await unitOfWork.ReadingListRepository.GetReadingListByIdAsync(readingListWithItems.Id, ReadingListIncludes.ItemChapter))?.Items;
        }
        if (items == null || items.Count == 0) return;

        if (items.First().Chapter == null)
        {
            logger.LogError("Tried to calculate release dates for Reading List, but missing Chapter entities");
            return;
        }
        var maxReleaseDate = items.Where(item => item.Chapter != null).Max(item => item.Chapter.ReleaseDate);
        var minReleaseDate = items.Where(item => item.Chapter != null).Min(item => item.Chapter.ReleaseDate);
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

    /// <summary>
    /// Calculates the highest Age Rating from each Reading List Item
    /// </summary>
    /// <remarks>This method is used when the ReadingList doesn't have items yet</remarks>
    /// <param name="readingList"></param>
    /// <param name="seriesIds">The series ids of all the reading list items</param>
    private async Task CalculateReadingListAgeRating(ReadingList readingList, IEnumerable<int> seriesIds)
    {
        var ageRating = await unitOfWork.SeriesRepository.GetMaxAgeRatingFromSeriesAsync(seriesIds);
        readingList.AgeRating = ageRating;
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
        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(username,
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
        return user.ReadingLists.Any(rl => rl.Id == readingListId) || await unitOfWork.UserRepository.IsUserAdminAsync(user);
    }

    /// <summary>
    /// Removes the Reading List from kavita
    /// </summary>
    /// <param name="readingListId"></param>
    /// <param name="user">User should have ReadingLists populated</param>
    /// <returns></returns>
    public async Task<bool> DeleteReadingList(int readingListId, AppUser user)
    {
        var readingList = await unitOfWork.ReadingListRepository.GetReadingListByIdAsync(readingListId);
        if (readingList == null) return true;
        user.ReadingLists.Remove(readingList);

        if (!unitOfWork.HasChanges()) return true;

        return await unitOfWork.CommitAsync();
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
        var chaptersForSeries = (await unitOfWork.ChapterRepository.GetChaptersByIdsAsync(chapterIds, ChapterIncludes.Volumes))
            .OrderBy(c => c.Volume.MinNumber)
            .ThenBy(x => x.SortOrder)
            .ToList();

        var index = readingList.Items.Count == 0 ? 0 : lastOrder + 1;
        foreach (var chapter in chaptersForSeries.Where(chapter => !existingChapterExists.Contains(chapter.Id)))
        {
            readingList.Items.Add(new ReadingListItemBuilder(index, seriesId, chapter.VolumeId, chapter.Id).Build());
            index += 1;
        }

        await CalculateReadingListAgeRating(readingList, new []{ seriesId });

        return index > lastOrder + 1;
    }

    /// <summary>
    /// Create Reading lists from a Series
    /// </summary>
    /// <remarks>Execute this from Hangfire</remarks>
    /// <param name="libraryId"></param>
    /// <param name="seriesId"></param>
    public async Task CreateReadingListsFromSeries(int libraryId, int seriesId)
    {
        var series = await unitOfWork.SeriesRepository.GetFullSeriesForSeriesIdAsync(seriesId);
        var library = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId);
        if (series == null || library == null) return;

        await CreateReadingListsFromSeries(series, library);
    }

    public async Task CreateReadingListsFromSeries(Series series, Library library)
    {
        if (!library.ManageReadingLists) return;

        var hasReadingListMarkers = series.Volumes
            .SelectMany(c => c.Chapters)
            .Any(c => !string.IsNullOrEmpty(c.StoryArc) || !string.IsNullOrEmpty(c.AlternateSeries));

        if (!hasReadingListMarkers) return;

        logger.LogInformation("Processing Reading Lists for {SeriesName}", series.Name);
        var user = await unitOfWork.UserRepository.GetDefaultAdminUser();
        series.Metadata ??= new SeriesMetadataBuilder().Build();

        foreach (var chapter in series.Volumes.SelectMany(v => v.Chapters))
        {
            var pairs = new List<Tuple<string, string>>();
            if (!string.IsNullOrEmpty(chapter.StoryArc))
            {
                pairs.AddRange(GeneratePairs(chapter.Files.FirstOrDefault()!.FilePath, chapter.StoryArc, chapter.StoryArcNumber));
            }
            if (!string.IsNullOrEmpty(chapter.AlternateSeries))
            {
                pairs.AddRange(GeneratePairs(chapter.Files.FirstOrDefault()!.FilePath, chapter.AlternateSeries, chapter.AlternateNumber));
            }

            foreach (var arcPair in pairs)
            {
                var readingList = await unitOfWork.ReadingListRepository.GetReadingListByTitleAsync(arcPair.Item1, user.Id);
                if (readingList == null)
                {
                    readingList = new ReadingListBuilder(arcPair.Item1)
                        .WithAppUserId(user.Id)
                        .Build();
                    unitOfWork.ReadingListRepository.Add(readingList);

                }

                var items = readingList.Items.ToList();
                var order = int.Parse(arcPair.Item2);
                var readingListItem = items.Find(item => item.Order == order || item.ChapterId == chapter.Id);
                if (readingListItem == null)
                {
                    // If no number was provided in the reading list, we default to MaxValue and hence we should insert the item at the end of the list
                    if (order == int.MaxValue)
                    {
                        order = items.Count > 0 ? items.Max(item => item.Order) + 1 : 0;
                    }
                    items.Add(new ReadingListItemBuilder(order, series.Id, chapter.VolumeId, chapter.Id).Build());
                }
                else
                {
                    if (order == int.MaxValue)
                    {
                        logger.LogWarning("{Filename} has a missing StoryArcNumber/AlternativeNumber but list already exists with this item. Skipping item", chapter.Files.FirstOrDefault()?.FilePath);
                    }
                    else
                    {
                        OrderableHelper.ReorderItems(items, readingListItem.Id, order);
                    }
                }

                readingList.Items = items;

                if (!unitOfWork.HasChanges()) continue;


                imageService.UpdateColorScape(readingList);
                await CalculateReadingListAgeRating(readingList);

                await unitOfWork.CommitAsync(); // TODO: See if we can avoid this extra commit by reworking bottom logic

                await CalculateStartAndEndDates((await unitOfWork.ReadingListRepository.GetReadingListByTitleAsync(
                    arcPair.Item1, user.Id, ReadingListIncludes.Items | ReadingListIncludes.ItemChapter))!);
                await unitOfWork.CommitAsync();
            }
        }
    }

    private IEnumerable<Tuple<string, string>> GeneratePairs(string filename, string storyArc, string storyArcNumbers)
    {
        var data = new List<Tuple<string, string>>();
        if (string.IsNullOrEmpty(storyArc)) return data;

        var arcs = storyArc.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var arcNumbers = storyArcNumbers.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (arcNumbers.Count(s => !string.IsNullOrEmpty(s)) != arcs.Length)
        {
            logger.LogWarning("There is a mismatch on StoryArc and StoryArcNumber for {FileName}", filename);
        }

        var maxPairs = Math.Max(arcs.Length, arcNumbers.Length);
        for (var i = 0; i < maxPairs; i++)
        {
            var arcNumber = int.MaxValue.ToString(CultureInfo.InvariantCulture);
            if (arcNumbers.Length > i)
            {
                arcNumber = arcNumbers[i];
            }

            if (string.IsNullOrEmpty(arcs[i]) || !int.TryParse(arcNumber, CultureInfo.InvariantCulture, out _)) continue;
            data.Add(new Tuple<string, string>(arcs[i], arcNumber));
        }

        return data;
    }

    /// <summary>
    /// Check for File issues like: No entries, Reading List Name collision, Duplicate Series across Libraries
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="cblReading"></param>
    /// <param name="useComicLibraryMatching">When true, will force ComicVine library naming conventions: Series (Year) for Series name matching.</param>
    public async Task<CblImportSummaryDto> ValidateCblFile(int userId, CblReadingList cblReading, bool useComicLibraryMatching = false)
    {
        var importSummary = new CblImportSummaryDto
        {
            CblName = cblReading.Name,
            Success = CblImportResult.Success,
            Results = [],
            SuccessfulInserts = []
        };

        if (IsCblEmpty(cblReading, importSummary, out var readingListFromCbl)) return readingListFromCbl;

        // Is there another reading list with the same name on the user's account?
        if (await unitOfWork.ReadingListRepository.ReadingListExistsForUser(cblReading.Name, userId))
        {
            importSummary.Success = CblImportResult.Fail;
            importSummary.Results.Add(new CblBookResult
            {
                Reason = CblImportReason.NameConflict,
                ReadingListName = cblReading.Name
            });
        }


        var uniqueSeries = GetUniqueSeries(cblReading, useComicLibraryMatching);
        var userSeries =
            (await unitOfWork.SeriesRepository.GetAllSeriesByNameAsync(uniqueSeries, userId, SeriesIncludes.Chapters)).ToList();

        if (userSeries.Count == 0)
        {
            // Report that no series exist in the reading list
            importSummary.Results.Add(new CblBookResult
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
            importSummary.Results.Add(new CblBookResult
            {
                Reason = CblImportReason.SeriesCollision,
                Series = conflict.Name,
                LibraryId = conflict.LibraryId,
                SeriesId = conflict.Id,
            });
        }

        return importSummary;
    }

    private static string GetSeriesFormatting(CblBook book, bool useComicLibraryMatching)
    {
        return useComicLibraryMatching ? $"{book.Series} ({book.Volume})" : book.Series;
    }

    private static List<string> GetUniqueSeries(CblReadingList cblReading, bool useComicLibraryMatching)
    {
        return cblReading.Books.Book.Select(b => Parser.Normalize(GetSeriesFormatting(b, useComicLibraryMatching))).Distinct().ToList();
    }


    /// <summary>
    /// Imports (or pretends to) a cbl into a reading list. Call <see cref="ValidateCblFile"/> first!
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="cblReading"></param>
    /// <param name="dryRun"></param>
    /// <param name="useComicLibraryMatching">When true, will force ComicVine library naming conventions: Series (Year) for Series name matching.</param>
    /// <returns></returns>
    public async Task<CblImportSummaryDto> CreateReadingListFromCbl(int userId, CblReadingList cblReading, bool dryRun = false, bool useComicLibraryMatching = false)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.ReadingListsWithItems);
        logger.LogDebug("Importing {ReadingListName} CBL for User {UserName}", cblReading.Name, user!.UserName);
        var importSummary = new CblImportSummaryDto
        {
            CblName = cblReading.Name,
            Success = CblImportResult.Success,
            Results = new List<CblBookResult>(),
            SuccessfulInserts = new List<CblBookResult>()
        };

        var uniqueSeries = GetUniqueSeries(cblReading, useComicLibraryMatching);
        var userSeries =
            (await unitOfWork.SeriesRepository.GetAllSeriesByNameAsync(uniqueSeries, userId, SeriesIncludes.Chapters)).ToList();
        var allSeries = userSeries.ToDictionary(s => s.NormalizedName);
        var allSeriesLocalized = userSeries.ToDictionary(s => s.NormalizedLocalizedName);

        var readingListNameNormalized = Parser.Normalize(cblReading.Name);

        // Get all the user's reading lists
        var allReadingLists = (user.ReadingLists).ToDictionary(s => s.NormalizedTitle);
        if (!allReadingLists.TryGetValue(readingListNameNormalized, out var readingList))
        {
            readingList = new ReadingListBuilder(cblReading.Name).WithSummary(cblReading.Summary).Build();
            user.ReadingLists.Add(readingList);
        }
        else
        {
            // Reading List exists, check if we own it
            if (user.ReadingLists.All(l => l.NormalizedTitle != readingListNameNormalized))
            {
                importSummary.Results.Add(new CblBookResult
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
            var normalizedSeries = Parser.Normalize(GetSeriesFormatting(book, useComicLibraryMatching));
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
                ? Parser.LooseLeafVolume
                : book.Volume;
            var matchingVolume = bookSeries.Volumes.Find(v => bookVolume == v.Name)
                                 ?? bookSeries.Volumes.GetLooseLeafVolumeOrDefault()
                                 ?? bookSeries.Volumes.GetSpecialVolumeOrDefault();
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

            // We need to handle default chapter or empty string when it's just a volume
            var bookNumber = string.IsNullOrEmpty(book.Number)
                ? Parser.DefaultChapter
                : book.Number;
            var chapter = matchingVolume.Chapters.FirstOrDefault(c => c.Range == bookNumber);
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
        if (!unitOfWork.HasChanges() || readingList.Items.Count == 0) return importSummary;


        imageService.UpdateColorScape(readingList);
        await unitOfWork.CommitAsync();


        return importSummary;
    }

    private static IList<Series> FindCblImportConflicts(IEnumerable<Series> userSeries)
    {
        var dict = new HashSet<string>();
        return userSeries.Where(series => !dict.Add(series.NormalizedName)).ToList();
    }

    private static bool IsCblEmpty(CblReadingList cblReading, CblImportSummaryDto importSummary,
        out CblImportSummaryDto readingListFromCbl)
    {
        readingListFromCbl = new CblImportSummaryDto();
        if (cblReading.Books == null || cblReading.Books.Book.Count == 0)
        {
            importSummary.Results.Add(new CblBookResult
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

        readingListItem = new ReadingListItemBuilder(readingList.Items.Count, seriesId,
            volumeId, chapterId).Build();
        readingList.Items.Add(readingListItem);
    }

    public static CblReadingList LoadCblFromPath(string path)
    {
        var reader = new XmlSerializer(typeof(CblReadingList));
        using var file = new StreamReader(path);
        var cblReadingList = (CblReadingList) reader.Deserialize(file);
        file.Close();
        return cblReadingList;
    }

    public async Task<string> GenerateReadingListCoverImage(int readingListId)
    {
        // TODO: Currently reading lists are dynamically generated at runtime. This needs to be overhauled to be generated and stored within
        // the Reading List (and just expire every so often) so we can utilize ColorScapes.
        // Check if a cover already exists for the reading list
        // var potentialExistingCoverPath = _directoryService.FileSystem.Path.Join(_directoryService.CoverImageDirectory,
        //     ImageService.GetReadingListFormat(readingListId));
        // if (_directoryService.FileSystem.File.Exists(potentialExistingCoverPath))
        // {
        //     // Check if we need to update CoverScape
        //
        // }

        var covers = await unitOfWork.ReadingListRepository.GetRandomCoverImagesAsync(readingListId);
        var destFile = directoryService.FileSystem.Path.Join(directoryService.TempDirectory,
            ImageService.GetReadingListFormat(readingListId));
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        destFile += settings.EncodeMediaAs.GetExtension();

        if (directoryService.FileSystem.File.Exists(destFile)) return destFile;
        ImageService.CreateMergedImage(
            covers.Select(c => directoryService.FileSystem.Path.Join(directoryService.CoverImageDirectory, c)).ToList(),
            settings.CoverImageSize,
            destFile);
        // TODO: Refactor this so that reading lists have a dedicated cover image so we can calculate primary/secondary colors

        return !directoryService.FileSystem.File.Exists(destFile) ? string.Empty : destFile;
    }

    public async Task UpdateReadingListAgeRatingForSeries(int seriesId, AgeRating ageRating)
    {
        var readingLists = await unitOfWork.ReadingListRepository.GetReadingListsBySeriesId(seriesId);
        foreach (var readingList in readingLists)
        {
            var seriesIds = readingList.Items.Select(item => item.SeriesId).ToList();
            seriesIds.Remove(seriesId); // Don't get AgeRating from database

            var maxAgeRating = await unitOfWork.SeriesRepository.GetMaxAgeRatingFromSeriesAsync(seriesIds);
            if (ageRating > maxAgeRating)
            {
                maxAgeRating = ageRating;
            }

            readingList.AgeRating = maxAgeRating;
        }
    }

    public async Task<IList<ReadingListItemDto>> GetReadingListItems(int readingListId, int userId, UserParams? userParams = null)
    {
        var items = await unitOfWork.ReadingListRepository.GetReadingListItemDtosByIdAsync(readingListId, userId, userParams);

        // Add the title
        foreach (var item in items)
        {
            item.Title = namingService.FormatReadingListItemTitle(item);
        }

        return items;
    }

    public async Task<ReadingListItemDto?> GetContinueReadingPoint(int readingListId, int userId)
    {
        var item = await unitOfWork.ReadingListRepository.GetContinueReadingPoint(readingListId, userId);
        item?.Title = namingService.FormatReadingListItemTitle(item);

        return item;
    }
}
