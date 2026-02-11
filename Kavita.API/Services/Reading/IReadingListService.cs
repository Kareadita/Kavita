using System.Collections.Generic;
using System.Threading.Tasks;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs.ReadingLists;
using Kavita.Models.DTOs.ReadingLists.CBL;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;

namespace Kavita.API.Services.Reading;

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

    Task<CblImportSummaryDto> ValidateCblFile(int userId, CblReadingList cblReading, bool useComicLibraryMatching = false);
    Task<CblImportSummaryDto> CreateReadingListFromCbl(int userId, CblReadingList cblReading, bool dryRun = false, bool useComicLibraryMatching = false);
    Task CalculateStartAndEndDates(ReadingList readingListWithItems);
    /// <summary>
    /// This is expected to be called from ProcessSeries and has the Full Series present. Will generate on the default admin user.
    /// </summary>
    /// <param name="series"></param>
    /// <param name="library"></param>
    /// <returns></returns>
    Task CreateReadingListsFromSeries(Series series, Library library);

    Task CreateReadingListsFromSeries(int libraryId, int seriesId);
    Task<string> GenerateReadingListCoverImage(int readingListId);
    /// <summary>
    /// Check, and update if needed, all reading lists' AgeRating who contain the passed series
    /// </summary>
    /// <param name="seriesId">The series whose age rating is being updated</param>
    /// <param name="ageRating">The new (uncommited) age rating of the series</param>
    /// <returns></returns>
    /// <remarks>This method does not commit changes</remarks>
    Task UpdateReadingListAgeRatingForSeries(int seriesId, AgeRating ageRating);

    Task<IList<ReadingListItemDto>> GetReadingListItems(int readingListId, int userId, UserParams? userParams = null);
    Task<ReadingListItemDto?> GetContinueReadingPoint(int readingListId, int userId);
}
