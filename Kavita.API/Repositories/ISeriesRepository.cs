using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Dashboard;
using Kavita.Models.DTOs.Filtering;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.DTOs.KavitaPlus.Metadata;
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
    [Obsolete("Use Dashboard")]
    Recommended = 3,
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
    Task<bool> DoesSeriesNameExistInLibrary(string name, int libraryId, MangaFormat format, CancellationToken ct = default);

    /// <summary>
    /// Adds user information like progress, ratings, etc
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="userId"></param>
    /// <param name="userParams">Pagination info</param>
    /// <param name="filter">Filtering/Sorting to apply</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<PagedList<SeriesDto>> GetSeriesDtoForLibraryIdAsync(int libraryId, int userId, UserParams userParams, FilterDto filter, CancellationToken ct = default);

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
    Task<SearchResultGroupDto> SearchSeries(int userId, bool isAdmin, IList<int> libraryIds, string searchQuery, bool includeChapterAndFiles = true, CancellationToken ct = default);
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
    Task<PagedList<SeriesDto>> GetOnDeck(int userId, int libraryId, UserParams userParams, FilterDto? filter, CancellationToken ct = default);
    Task<PagedList<SeriesDto>> GetRecentlyAdded(int libraryId, int userId, UserParams userParams, FilterDto filter, CancellationToken ct = default);
    Task<PagedList<SeriesDto>> GetRecentlyAddedV2(int userId, UserParams userParams, FilterV2Dto filter, CancellationToken ct = default);
    Task<SeriesMetadataDto?> GetSeriesMetadata(int seriesId, CancellationToken ct = default);
    Task<PagedList<SeriesDto>> GetSeriesDtoForCollectionAsync(int collectionId, int userId, UserParams userParams, CancellationToken ct = default);
    Task<IList<MangaFile>> GetFilesForSeries(int seriesId, CancellationToken ct = default);
    Task<IEnumerable<SeriesDto>> GetSeriesDtoForIdsAsync(IEnumerable<int> seriesIds, int userId, CancellationToken ct = default);
    Task<IList<string>> GetAllCoverImagesAsync(CancellationToken ct = default);
    Task<IEnumerable<string>> GetLockedCoverImagesAsync(CancellationToken ct = default);
    Task<PagedList<Series>> GetFullSeriesForLibraryIdAsync(int libraryId, UserParams userParams, CancellationToken ct = default);
    Task<Series?> GetFullSeriesForSeriesIdAsync(int seriesId, CancellationToken ct = default);
    Task<Chunk> GetChunkInfo(int libraryId = 0, CancellationToken ct = default);
    Task<IList<GroupedSeriesDto>> GetRecentlyUpdatedSeries(int userId, UserParams? userParams, CancellationToken ct = default);
    Task<RelatedSeriesDto> GetRelatedSeries(int userId, int seriesId, CancellationToken ct = default);
    Task<IEnumerable<SeriesDto>> GetSeriesForRelationKind(int userId, int seriesId, RelationKind kind, CancellationToken ct = default);
    Task<PagedList<SeriesDto>> GetQuickReads(int userId, int libraryId, UserParams userParams, CancellationToken ct = default);
    Task<PagedList<SeriesDto>> GetQuickCatchupReads(int userId, int libraryId, UserParams userParams, CancellationToken ct = default);
    Task<PagedList<SeriesDto>> GetHighlyRated(int userId, int libraryId, UserParams userParams, CancellationToken ct = default);
    Task<PagedList<SeriesDto>> GetMoreIn(int userId, int libraryId, int genreId, UserParams userParams, CancellationToken ct = default);
    Task<PagedList<SeriesDto>> GetRediscover(int userId, int libraryId, UserParams userParams, CancellationToken ct = default);
    Task<SeriesDto?> GetSeriesForMangaFile(int mangaFileId, int userId, CancellationToken ct = default);
    Task<SeriesDto?> GetSeriesForChapter(int chapterId, int userId, CancellationToken ct = default);
    Task<PagedList<SeriesDto>> GetWantToReadForUserAsync(int userId, UserParams userParams, FilterDto filter, CancellationToken ct = default);
    Task<PagedList<SeriesDto>> GetWantToReadForUserV2Async(int userId, UserParams userParams, FilterV2Dto filter, CancellationToken ct = default);
    Task<IList<Series>> GetWantToReadForUserAsync(int userId, CancellationToken ct = default);
    Task<bool> IsSeriesInWantToRead(int userId, int seriesId, CancellationToken ct = default);
    Task<Series?> GetSeriesByFolderPath(string folder, SeriesIncludes includes = SeriesIncludes.None, CancellationToken ct = default);
    Task<Series?> GetSeriesThatContainsLowestFolderPath(string path, SeriesIncludes includes = SeriesIncludes.None, CancellationToken ct = default);
    Task<IEnumerable<Series>> GetAllSeriesByNameAsync(IList<string> normalizedNames,
        int userId, SeriesIncludes includes = SeriesIncludes.None, CancellationToken ct = default);
    Task<Series?> GetFullSeriesByAnyName(string seriesName, string localizedName, int libraryId, MangaFormat format, bool withFullIncludes = true, CancellationToken ct = default);
    Task<Series?> GetSeriesByAnyName(IList<string> names, IList<MangaFormat> formats,
        int userId, int? aniListId = null, SeriesIncludes includes = SeriesIncludes.None, CancellationToken ct = default);
    Task<Series?> GetSeriesByAnyName(string seriesName, string localizedName, IList<MangaFormat> formats, int userId, int? aniListId = null, SeriesIncludes includes = SeriesIncludes.None, CancellationToken ct = default);
    public Task<IList<Series>> GetAllSeriesByAnyName(string seriesName, string localizedName, int libraryId,
        MangaFormat format, CancellationToken ct = default);
    Task<IList<Series>> RemoveSeriesNotInList(IList<ParsedSeries> seenSeries, int libraryId, CancellationToken ct = default);
    Task<IDictionary<string, IList<SeriesModified>>> GetFolderPathMap(int libraryId, CancellationToken ct = default);
    Task<AgeRating> GetMaxAgeRatingFromSeriesAsync(IEnumerable<int> seriesIds, CancellationToken ct = default);
    Task<IList<SeriesMetadataDto>> GetSeriesMetadataForIds(IEnumerable<int> seriesIds, CancellationToken ct = default);
    Task<IList<Series>> GetAllWithCoversInDifferentEncoding(EncodeFormat encodeFormat, bool customOnly = true, CancellationToken ct = default);
    Task<SeriesDto?> GetSeriesDtoByNamesAndMetadataIds(IEnumerable<string> names, LibraryType libraryType, string aniListUrl, string malUrl, CancellationToken ct = default);
    Task<int> GetAverageUserRating(int seriesId, int userId, CancellationToken ct = default);
    Task RemoveFromOnDeck(int seriesId, int userId, CancellationToken ct = default);
    Task ClearOnDeckRemoval(int seriesId, int userId, CancellationToken ct = default);
    Task<PagedList<SeriesDto>> GetSeriesDtoForLibraryIdV2Async(int userId, UserParams userParams, FilterV2Dto filterDto, QueryContext queryContext = QueryContext.None, CancellationToken ct = default);
    Task<PlusSeriesRequestDto?> GetPlusSeriesDto(int seriesId, CancellationToken ct = default);
    Task<Series?> MatchSeries(ExternalSeriesDetailDto externalSeries, CancellationToken ct = default);
}
