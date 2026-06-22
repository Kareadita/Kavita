using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Dashboard;
using Kavita.Models.DTOs.Filtering;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.DTOs.Filtering.v2.Requests;
using Kavita.Models.DTOs.KavitaPlus.Metadata;
using Kavita.Models.DTOs.KavitaPlus.Scrobble;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.DTOs.Search;
using Kavita.Models.DTOs.SeriesDetail;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Metadata;
using Kavita.Models.Entities.User;
using Kavita.Models.Misc;
using Kavita.Models.Parser;

namespace Kavita.API.Repositories;

[Flags]
public enum SeriesIncludes
{
    None = 1 << 0,
    Volumes = 1 << 1,
    /// <summary>
    /// This will include all necessary includes
    /// </summary>
    Metadata = 1 << 2,
    Related = 1 << 3,
    Library = 1 << 4,
    Chapters = 1 << 5,
    ExternalReviews = 1 << 6,
    ExternalRatings = 1 << 7,
    ExternalRecommendations = 1 << 8,
    ExternalMetadata = 1 << 9,

    ExternalData = ExternalMetadata | ExternalReviews | ExternalRatings | ExternalRecommendations,
}

/// <summary>
/// For complex queries, Library has certain restrictions where the library should not be included in results.
/// This enum dictates which field to use for the lookup.
/// </summary>
public enum QueryContext
{
    None = 1,
    Search = 2,
    Dashboard = 4,
}

public interface ISeriesRepository
{
    void Add(Series series);
    void Attach(SeriesRelation relation);
    void Update(Series series);
    void Update(SeriesMetadata seriesMetadata);
    void Remove(Series series);
    void Remove(IEnumerable<Series> series);
    Task<bool> DoesSeriesNameExistInLibraryAsync(string name, int libraryId, MangaFormat format, CancellationToken ct = default);
    /// <summary>
    /// Does not add user information like progress, ratings, etc.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="isAdmin"></param>
    /// <param name="libraryIds"></param>
    /// <param name="searchQuery"></param>
    /// <param name="includeChapterAndFiles">Includes Files in the Search</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<SearchResultGroupDto> SearchSeriesAsync(int userId, bool isAdmin, IList<int> libraryIds, string searchQuery, bool includeChapterAndFiles = true, CancellationToken ct = default);
    Task<IEnumerable<Series>> GetSeriesForLibraryIdAsync(int libraryId, SeriesIncludes includes = SeriesIncludes.None, CancellationToken ct = default);
    Task<SeriesDto?> GetSeriesDtoByIdAsync(int seriesId, int userId, CancellationToken ct = default);
    Task<Series?> GetSeriesByIdAsync(int seriesId, SeriesIncludes includes = SeriesIncludes.Volumes | SeriesIncludes.Metadata, CancellationToken ct = default);
    Task<IList<SeriesDto>> GetSeriesDtoByIdsAsync(IEnumerable<int> seriesIds, AppUser user, CancellationToken ct = default);
    Task<IList<Series>> GetSeriesByIdsAsync(IList<int> seriesIds, bool fullSeries = true, CancellationToken ct = default);
    Task<int[]> GetChapterIdsForSeriesAsync(IList<int> seriesIds, CancellationToken ct = default);
    Task<IDictionary<int, IList<int>>> GetChapterIdWithSeriesIdForSeriesAsync(int[] seriesIds, CancellationToken ct = default);
    Task<long> GetFilesizeAsync(int seriesId, CancellationToken ct = default);
    Task<Dictionary<int, long>> GetFilesizesAsync(IList<int> seriesIds, CancellationToken ct = default);
    Task<string?> GetSeriesCoverImageAsync(int seriesId, CancellationToken ct = default);
    Task<PagedList<SeriesDto>> GetOnDeckAsync(int userId, int libraryId, UserParams userParams, CancellationToken ct = default);
    Task<PagedList<SeriesDto>> GetRecentlyAddedAsync(int userId, UserParams userParams, SeriesFilterV2Dto seriesFilter, CancellationToken ct = default);
    Task<SeriesMetadataDto?> GetSeriesMetadataAsync(int seriesId, CancellationToken ct = default);
    Task<PagedList<SeriesDto>> GetSeriesDtoForCollectionAsync(int collectionId, int userId, UserParams userParams, CancellationToken ct = default);
    Task<IList<MangaFile>> GetFilesForSeriesAsync(int seriesId, CancellationToken ct = default);
    Task<IEnumerable<SeriesDto>> GetSeriesDtoForIdsAsync(IEnumerable<int> seriesIds, int userId, CancellationToken ct = default);
    Task<IList<string>> GetAllCoverImagesAsync(CancellationToken ct = default);
    Task<IEnumerable<string>> GetLockedCoverImagesAsync(CancellationToken ct = default);
    Task<PagedList<Series>> GetFullSeriesForLibraryIdAsync(int libraryId, UserParams userParams, CancellationToken ct = default);
    Task<Series?> GetFullSeriesForSeriesIdAsync(int seriesId, CancellationToken ct = default);
    Task<Chunk> GetChunkInfoAsync(int libraryId = 0, CancellationToken ct = default);
    Task<IList<GroupedSeriesDto>> GetRecentlyUpdatedSeriesAsync(int userId, UserParams? userParams, CancellationToken ct = default);
    Task<RelatedSeriesDto> GetRelatedSeriesAsync(int userId, int seriesId, CancellationToken ct = default);
    Task<IEnumerable<SeriesDto>> GetSeriesForRelationKindAsync(int userId, int seriesId, RelationKind kind, CancellationToken ct = default);
    Task<SeriesDto?> GetSeriesForMangaFileAsync(int mangaFileId, int userId, CancellationToken ct = default);
    Task<SeriesDto?> GetSeriesForChapterAsync(int chapterId, int userId, CancellationToken ct = default);
    Task<PagedList<SeriesDto>> GetWantToReadDtosForUserAsync(int userId, UserParams userParams, SeriesFilterV2Dto seriesFilter, CancellationToken ct = default);
    Task<IList<Series>> GetWantToReadForUserAsync(int userId, CancellationToken ct = default);
    Task<bool> IsSeriesInWantToRead(int userId, int seriesId, CancellationToken ct = default);
    Task<Series?> GetSeriesByFolderPathAsync(string folder, SeriesIncludes includes = SeriesIncludes.None, CancellationToken ct = default);
    Task<Series?> GetSeriesThatContainsLowestFolderPathAsync(string path, SeriesIncludes includes = SeriesIncludes.None, CancellationToken ct = default);
    Task<IEnumerable<Series>> GetAllSeriesByNameAsync(IList<string> normalizedNames,
        int userId, SeriesIncludes includes = SeriesIncludes.None, CancellationToken ct = default);
    Task<Series?> GetFullSeriesByAnyName(string seriesName, string localizedName, int libraryId, MangaFormat format, bool withFullIncludes = true, CancellationToken ct = default);
    Task<Series?> GetSeriesByAnyNameAsync(IList<string> names, IList<MangaFormat> formats,
        int userId, int? aniListId = null, SeriesIncludes includes = SeriesIncludes.None, CancellationToken ct = default);
    Task<Series?> GetSeriesByAnyNameAsync(string seriesName, string localizedName, IList<MangaFormat> formats, int userId, int? aniListId = null, SeriesIncludes includes = SeriesIncludes.None, CancellationToken ct = default);
    public Task<IList<Series>> GetAllSeriesByAnyNameAsync(string seriesName, string localizedName, int libraryId,
        MangaFormat format, CancellationToken ct = default);
    Task<IList<Series>> RemoveSeriesNotInListAsync(IList<ParsedSeries> seenSeries, int libraryId, CancellationToken ct = default);
    Task<IDictionary<string, IList<SeriesModified>>> GetFolderPathMapAsync(int libraryId, CancellationToken ct = default);
    Task<AgeRating> GetMaxAgeRatingFromSeriesAsyncAsync(IEnumerable<int> seriesIds, CancellationToken ct = default);
    Task<IList<SeriesMetadataDto>> GetSeriesMetadataForIdsAsync(IEnumerable<int> seriesIds, CancellationToken ct = default);
    Task<IList<Series>> GetAllWithCoversInDifferentEncodingAsync(EncodeFormat encodeFormat, bool customOnly = true, CancellationToken ct = default);
    Task<SeriesDto?> GetSeriesDtoByNamesAndMetadataIdsAsync(IEnumerable<string> names, LibraryType libraryType, string aniListUrl, string malUrl, CancellationToken ct = default);
    Task<int> GetAverageUserRatingAsync(int seriesId, int userId, CancellationToken ct = default);
    Task RemoveFromOnDeckAsync(int seriesId, int userId, CancellationToken ct = default);
    Task ClearOnDeckRemovalAsync(int seriesId, int userId, CancellationToken ct = default);
    Task<PagedList<SeriesDto>> GetSeriesDtoForLibraryIdAsync(int userId, UserParams userParams, SeriesFilterV2Dto seriesFilterDto, QueryContext queryContext = QueryContext.None, CancellationToken ct = default);
    Task<PlusSeriesRequestDto?> GetPlusSeriesDtoAsync(int seriesId, CancellationToken ct = default);
    Task<Series?> MatchSeriesAsync(ExternalSeriesDetailDto externalSeries, CancellationToken ct = default);
    Task<List<Series>> GetSeriesForReadStatusTransitionRuleAsync(int userId, ReadStatusTransitionRule rule, bool requireUnReadChapters, CancellationToken ct);
}
