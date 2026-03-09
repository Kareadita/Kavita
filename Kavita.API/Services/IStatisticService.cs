using System;
using System.Collections.Generic;
using System.Threading;
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
    Task<ServerStatisticsDto> GetServerStatistics(CancellationToken ct = default);
    Task<UserReadStatistics> GetUserReadStatistics(int userId, IList<int> libraryIds, CancellationToken ct = default);
    Task<IEnumerable<StatCount<int>>> GetYearCount(CancellationToken ct = default);
    Task<IEnumerable<StatCount<int>>> GetTopYears(CancellationToken ct = default);
    Task<IList<StatBucketDto>> GetPopularDecades(CancellationToken ct = default);
    Task<IList<StatCount<LibraryDto>>> GetPopularLibraries(CancellationToken ct = default);
    Task<IList<StatCount<SeriesDto>>> GetPopularSeries(CancellationToken ct = default);
    Task<IList<StatCount<ReadingListDto>>> GetPopularReadingList(int take = 5, CancellationToken ct = default);
    Task<IList<StatCount<GenreTagDto>>> GetPopularGenres(CancellationToken ct = default);
    Task<IList<StatCount<TagDto>>> GetPopularTags(CancellationToken ct = default);
    Task<IList<StatCount<PersonDto>>> GetPopularPerson(PersonRole role, CancellationToken ct = default);
    Task<IEnumerable<StatCount<PublicationStatus>>> GetPublicationCount(CancellationToken ct = default);
    Task<IEnumerable<StatCount<MangaFormat>>> GetMangaFormatCount(CancellationToken ct = default);
    Task<FileExtensionBreakdownDto> GetFileBreakdown(CancellationToken ct = default);
    Task<IEnumerable<TopReadDto>> GetTopUsers(int days, CancellationToken ct = default);
    Task<IEnumerable<ReadHistoryEvent>> GetReadingHistory(int userId, CancellationToken ct = default);
    Task<IEnumerable<StatCountWithFormat<DateTime>>> ReadCountByDay(int userId = 0, int days = 0, CancellationToken ct = default);
    Task<IEnumerable<StatCountWithFormat<DateTime>>> ReadCounts(StatsFilterDto filter, int userId = 0, CancellationToken ct = default);
    Task<IList<StatCount<DayOfWeek>>> GetDayBreakdown(int userId = 0, CancellationToken ct = default);
    Task<IList<StatCount<int>>> GetPagesReadCountByYear(int userId = 0, CancellationToken ct = default);
    Task<IList<StatCount<int>>> GetWordsReadCountByYear(int userId = 0, CancellationToken ct = default);
    Task UpdateServerStatistics(CancellationToken ct = default);
    Task<IEnumerable<FileExtensionExportDto>> GetFilesByExtension(string fileExtension, CancellationToken ct = default);
    Task<DeviceClientBreakdownDto> GetClientTypeBreakdown(DateTime fromDateUtc, CancellationToken ct = default);
    Task<IList<StatCount<string>>> GetDeviceTypeCounts(DateTime fromDateUtc, CancellationToken ct = default);
    Task<ReadingActivityGraphDto> GetReadingActivityGraphData(StatsFilterDto filter, int userId, int year, int requestingUserId, CancellationToken ct = default);
    Task<ReadingPaceDto> GetReadingPaceForUser(StatsFilterDto filter, int userId, int year, bool booksOnly, int requestingUserId, CancellationToken ct = default);
    Task<BreakDownDto<string>> GetGenreBreakdownForUser(StatsFilterDto filter, int userId, int requestingUserId, CancellationToken ct = default);
    Task<BreakDownDto<string>> GetTagBreakdownForUser(StatsFilterDto filter, int userId, int requestingUserId, CancellationToken ct = default);
    Task<SpreadStatsDto> GetPageSpreadForUser(StatsFilterDto filter, int userId, int requestingUserId, CancellationToken ct = default);
    Task<SpreadStatsDto> GetWordSpreadForUser(StatsFilterDto filter, int userId, int requestingUserId, CancellationToken ct = default);
    Task<IList<StatCount<YearMonthGroupingDto>>> GetReadsPerMonth(StatsFilterDto filter, int userId, int requestingUserId, CancellationToken ct = default);
    Task<IList<MostReadAuthorsDto>> GetMostReadAuthors(StatsFilterDto filter, int userId, int requestingUserId, CancellationToken ct = default);
    Task<int> GetTotalReads(int userId, int requestingUserId, CancellationToken ct = default);
    Task<ReadTimeByHourDto?> GetTimeReadingByHour(StatsFilterDto filter, int userId, int requestingUserId, CancellationToken ct = default);
    Task<ProfileStatBarDto> GetUserStatBar(StatsFilterDto filter, int userId, int requestingUserId, CancellationToken ct = default);
    Task<IList<MostActiveUserDto>> GetMostActiveUsers(StatsFilterDto filter, CancellationToken ct = default);
    Task<IList<StatCountWithFormat<DateTime>>> GetFilesAddedOverTime(CancellationToken ct = default);
    Task<PagedList<ReadingHistoryItemDto>> GetReadingHistoryItems(StatsFilterDto filter, UserParams userParams, int userId, int requestingUserId, CancellationToken ct = default);
}
