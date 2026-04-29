using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.ReadingLists;
using Kavita.API.Services.SignalR;
using Kavita.Common;
using Kavita.Common.Extensions;
using Kavita.Common.Helpers;
using Kavita.Models.Builders;
using Kavita.Models.DTOs.ReadingLists;
using Kavita.Models.DTOs.ReadingLists.Request;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.ReadingLists;
using Kavita.Models.Entities.User;
using Kavita.Models.Extensions;
using Kavita.Models.Helpers;
using Kavita.Services.Helpers;
using Kavita.Services.Reading;
using Kavita.Services.Scanner;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.ReadingLists;

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

        if (readingList.Tags == null)
        {
            throw new ArgumentException("You must pass a Reading List with tags included");
        }

        await TagHelper.UpdateEntityTags(readingList.Tags, dto.Tags, unitOfWork.DataContext.ReadingListTag, unitOfWork, false);


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
        var ageRating = await unitOfWork.SeriesRepository.GetMaxAgeRatingFromSeriesAsyncAsync(seriesIds);
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
        if (readingList.Items.Count != 0)
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

        await CalculateReadingListAgeRating(readingList, [seriesId]);

        return index > lastOrder + 1;
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


    public async Task<string> GenerateReadingListCoverImage(int readingListId, bool commit = false)
    {
        var readingList = await unitOfWork.ReadingListRepository.GetReadingListByIdAsync(readingListId);
        if (readingList == null) return string.Empty;

        var path =  await GenerateReadingListCoverImage(readingList);

        if (!commit) return path;

        await unitOfWork.CommitAsync();

        await eventHub.SendMessageAsync(MessageFactory.CoverUpdate,
            MessageFactory.CoverUpdateEvent(readingListId, MessageFactoryEntityTypes.ReadingList), false);

        return path;
    }

    public async Task<string> GenerateReadingListCoverImage(ReadingList readingList)
    {
        var covers = await unitOfWork.ReadingListRepository.GetRandomCoverImagesAsync(readingList.Id);
        if (covers.Count == 0) return string.Empty;

        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        var fileName = ImageService.GetReadingListFormat(readingList.Id);
        var destFile = directoryService.FileSystem.Path.Join(directoryService.CoverImageDirectory,
            fileName + settings.EncodeMediaAs.GetExtension());

        ImageService.CreateMergedImage(
            covers.Select(c => directoryService.FileSystem.Path.Join(directoryService.CoverImageDirectory, c)).ToList(),
            settings.CoverImageSize,
            destFile);

        if (!directoryService.FileSystem.File.Exists(destFile)) return string.Empty;

        readingList.CoverImage = fileName + settings.EncodeMediaAs.GetExtension();
        imageService.UpdateColorScape(readingList);

        return readingList.CoverImage;
    }

    /// <summary>
    /// Generates a cover image for the reading list if it has more than 3 items and doesn't already have a locked/set cover.
    /// </summary>
    /// <remarks>Commits changes if a cover was generated</remarks>
    public async Task UpdateReadingListCoverImage(ReadingList readingList)
    {
        if (readingList.CoverImageLocked || !string.IsNullOrEmpty(readingList.CoverImage)) return;
        if (readingList.Items == null || readingList.Items.Count < 4) return;

        var coverImage = await GenerateReadingListCoverImage(readingList);
        if (!string.IsNullOrEmpty(coverImage))
        {
            await unitOfWork.CommitAsync();
        }
    }

    public async Task UpdateReadingListAgeRatingForSeries(int seriesId, AgeRating ageRating)
    {
        var readingLists = await unitOfWork.ReadingListRepository.GetReadingListsBySeriesId(seriesId);
        foreach (var readingList in readingLists)
        {
            var seriesIds = readingList.Items.Select(item => item.SeriesId).ToList();
            seriesIds.Remove(seriesId); // Don't get AgeRating from database

            var maxAgeRating = await unitOfWork.SeriesRepository.GetMaxAgeRatingFromSeriesAsyncAsync(seriesIds);
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
}
