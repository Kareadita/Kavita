using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Metadata;
using Kavita.Models.DTOs.Person;
using Kavita.Models.DTOs.ReadingLists;
using Kavita.Models.DTOs.Statistics;
using Kavita.Models.DTOs.Stats;
using Kavita.Models.DTOs.Stats.V3.ClientDevice;
using Kavita.Models.Entities.Enums;

namespace Kavita.API.Services;

public interface IStatisticService
{
    Task<ServerStatisticsDto> GetServerStatistics();
    Task<UserReadStatistics> GetUserReadStatistics(int userId, IList<int> libraryIds);
    Task<IEnumerable<StatCount<int>>> GetYearCount();
    Task<IEnumerable<StatCount<int>>> GetTopYears();
    Task<IList<StatBucketDto>> GetPopularDecades();
    Task<IList<StatCount<LibraryDto>>> GetPopularLibraries();
    Task<IList<StatCount<SeriesDto>>> GetPopularSeries();
    Task<IList<StatCount<ReadingListDto>>> GetPopularReadingList(int take = 5);
    Task<IList<StatCount<GenreTagDto>>> GetPopularGenres();
    Task<IList<StatCount<TagDto>>> GetPopularTags();
    Task<IList<StatCount<PersonDto>>> GetPopularPerson(PersonRole role);
    Task<IEnumerable<StatCount<PublicationStatus>>> GetPublicationCount();
    Task<IEnumerable<StatCount<MangaFormat>>> GetMangaFormatCount();
    Task<FileExtensionBreakdownDto> GetFileBreakdown();
    Task<IEnumerable<TopReadDto>> GetTopUsers(int days);
    Task<IEnumerable<ReadHistoryEvent>> GetReadingHistory(int userId);
    Task<IEnumerable<StatCountWithFormat<DateTime>>> ReadCountByDay(int userId = 0, int days = 0);
    Task<IEnumerable<StatCountWithFormat<DateTime>>> ReadCounts(StatsFilterDto filter, int userId = 0);
    Task<IList<StatCount<DayOfWeek>>> GetDayBreakdown(int userId = 0);
    Task<IList<StatCount<int>>> GetPagesReadCountByYear(int userId = 0);
    Task<IList<StatCount<int>>> GetWordsReadCountByYear(int userId = 0);
    Task UpdateServerStatistics();
    Task<IEnumerable<FileExtensionExportDto>> GetFilesByExtension(string fileExtension);
    Task<DeviceClientBreakdownDto> GetClientTypeBreakdown(DateTime fromDateUtc);
    Task<IList<StatCount<string>>> GetDeviceTypeCounts(DateTime fromDateUtc);
    Task<ReadingActivityGraphDto> GetReadingActivityGraphData(StatsFilterDto filter, int userId, int year, int requestingUserId);
    Task<ReadingPaceDto> GetReadingPaceForUser(StatsFilterDto filter, int userId, int year, bool booksOnly, int requestingUserId);
    Task<BreakDownDto<string>> GetGenreBreakdownForUser(StatsFilterDto filter, int userId, int requestingUserId);
    Task<BreakDownDto<string>> GetTagBreakdownForUser(StatsFilterDto filter, int userId, int requestingUserId);
    Task<SpreadStatsDto> GetPageSpreadForUser(StatsFilterDto filter, int userId, int requestingUserId);
    Task<SpreadStatsDto> GetWordSpreadForUser(StatsFilterDto filter, int userId, int requestingUserId);
    Task<IList<StatCount<YearMonthGroupingDto>>> GetReadsPerMonth(StatsFilterDto filter, int userId, int requestingUserId);
    Task<IList<MostReadAuthorsDto>> GetMostReadAuthors(StatsFilterDto filter, int userId, int requestingUserId);
    Task<int> GetTotalReads(int userId, int requestingUserId);
    Task<ReadTimeByHourDto?> GetTimeReadingByHour(StatsFilterDto filter, int userId, int requestingUserId);
    Task<ProfileStatBarDto> GetUserStatBar(StatsFilterDto filter, int userId, int requestingUserId);
    Task<IList<MostActiveUserDto>> GetMostActiveUsers(StatsFilterDto filter);
    Task<IList<StatCountWithFormat<DateTime>>> GetFilesAddedOverTime();
    Task<PagedList<ReadingHistoryItemDto>> GetReadingHistoryItems(StatsFilterDto filter, UserParams userParams, int userId, int requestingUserId);
}
