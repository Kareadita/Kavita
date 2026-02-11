using System;
using System.Collections.Generic;
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
    None = 1,
    Volumes = 2,
    /// <summary>
    /// This will include all necessary includes
    /// </summary>
    Metadata = 4,
    Related = 8,
    Library = 16,
    Chapters = 32,
    ExternalReviews = 64,
    ExternalRatings = 128,
    ExternalRecommendations = 256,
    ExternalMetadata = 512,

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
    Task<bool> DoesSeriesNameExistInLibrary(string name, int libraryId, MangaFormat format);
    /// <summary>
    /// Adds user information like progress, ratings, etc
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="userId"></param>
    /// <param name="userParams">Pagination info</param>
    /// <param name="filter">Filtering/Sorting to apply</param>
    /// <returns></returns>
    Task<PagedList<SeriesDto>> GetSeriesDtoForLibraryIdAsync(int libraryId, int userId, UserParams userParams, FilterDto filter);
    /// <summary>
    /// Does not add user information like progress, ratings, etc.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="isAdmin"></param>
    /// <param name="libraryIds"></param>
    /// <param name="searchQuery"></param>
    /// <param name="includeChapterAndFiles">Includes Files in the Search</param>
    /// <returns></returns>
    Task<SearchResultGroupDto> SearchSeries(int userId, bool isAdmin, IList<int> libraryIds, string searchQuery, bool includeChapterAndFiles = true);
    Task<IEnumerable<Series>> GetSeriesForLibraryIdAsync(int libraryId, SeriesIncludes includes = SeriesIncludes.None);
    Task<SeriesDto?> GetSeriesDtoByIdAsync(int seriesId, int userId);
    Task<Series?> GetSeriesByIdAsync(int seriesId, SeriesIncludes includes = SeriesIncludes.Volumes | SeriesIncludes.Metadata);
    Task<IList<SeriesDto>> GetSeriesDtoByIdsAsync(IEnumerable<int> seriesIds, AppUser user);
    Task<IList<Series>> GetSeriesByIdsAsync(IList<int> seriesIds, bool fullSeries = true);
    Task<int[]> GetChapterIdsForSeriesAsync(IList<int> seriesIds);
    Task<IDictionary<int, IList<int>>> GetChapterIdWithSeriesIdForSeriesAsync(int[] seriesIds);
    Task<string?> GetSeriesCoverImageAsync(int seriesId);
    Task<PagedList<SeriesDto>> GetOnDeck(int userId, int libraryId, UserParams userParams, FilterDto? filter);
    Task<PagedList<SeriesDto>> GetRecentlyAdded(int libraryId, int userId, UserParams userParams, FilterDto filter);
    Task<PagedList<SeriesDto>> GetRecentlyAddedV2(int userId, UserParams userParams, FilterV2Dto filter);
    Task<SeriesMetadataDto?> GetSeriesMetadata(int seriesId);
    Task<PagedList<SeriesDto>> GetSeriesDtoForCollectionAsync(int collectionId, int userId, UserParams userParams);
    Task<IList<MangaFile>> GetFilesForSeries(int seriesId);
    Task<IEnumerable<SeriesDto>> GetSeriesDtoForIdsAsync(IEnumerable<int> seriesIds, int userId);
    Task<IList<string>> GetAllCoverImagesAsync();
    Task<IEnumerable<string>> GetLockedCoverImagesAsync();
    Task<PagedList<Series>> GetFullSeriesForLibraryIdAsync(int libraryId, UserParams userParams);
    Task<Series?> GetFullSeriesForSeriesIdAsync(int seriesId);
    Task<Chunk> GetChunkInfo(int libraryId = 0);
    Task<IList<GroupedSeriesDto>> GetRecentlyUpdatedSeries(int userId, UserParams? userParams);
    Task<RelatedSeriesDto> GetRelatedSeries(int userId, int seriesId);
    Task<IEnumerable<SeriesDto>> GetSeriesForRelationKind(int userId, int seriesId, RelationKind kind);
    Task<PagedList<SeriesDto>> GetQuickReads(int userId, int libraryId, UserParams userParams);
    Task<PagedList<SeriesDto>> GetQuickCatchupReads(int userId, int libraryId, UserParams userParams);
    Task<PagedList<SeriesDto>> GetHighlyRated(int userId, int libraryId, UserParams userParams);
    Task<PagedList<SeriesDto>> GetMoreIn(int userId, int libraryId, int genreId, UserParams userParams);
    Task<PagedList<SeriesDto>> GetRediscover(int userId, int libraryId, UserParams userParams);
    Task<SeriesDto?> GetSeriesForMangaFile(int mangaFileId, int userId);
    Task<SeriesDto?> GetSeriesForChapter(int chapterId, int userId);
    Task<PagedList<SeriesDto>> GetWantToReadForUserAsync(int userId, UserParams userParams, FilterDto filter);
    Task<PagedList<SeriesDto>> GetWantToReadForUserV2Async(int userId, UserParams userParams, FilterV2Dto filter);
    Task<IList<Series>> GetWantToReadForUserAsync(int userId);
    Task<bool> IsSeriesInWantToRead(int userId, int seriesId);
    Task<Series?> GetSeriesByFolderPath(string folder, SeriesIncludes includes = SeriesIncludes.None);
    Task<Series?> GetSeriesThatContainsLowestFolderPath(string path, SeriesIncludes includes = SeriesIncludes.None);
    Task<IEnumerable<Series>> GetAllSeriesByNameAsync(IList<string> normalizedNames,
        int userId, SeriesIncludes includes = SeriesIncludes.None);
    Task<Series?> GetFullSeriesByAnyName(string seriesName, string localizedName, int libraryId, MangaFormat format, bool withFullIncludes = true);
    Task<Series?> GetSeriesByAnyName(IList<string> names, IList<MangaFormat> formats,
        int userId, int? aniListId = null, SeriesIncludes includes = SeriesIncludes.None);
    Task<Series?> GetSeriesByAnyName(string seriesName, string localizedName, IList<MangaFormat> formats, int userId, int? aniListId = null, SeriesIncludes includes = SeriesIncludes.None);
    public Task<IList<Series>> GetAllSeriesByAnyName(string seriesName, string localizedName, int libraryId,
        MangaFormat format);
    Task<IList<Series>> RemoveSeriesNotInList(IList<ParsedSeries> seenSeries, int libraryId);
    Task<IDictionary<string, IList<SeriesModified>>> GetFolderPathMap(int libraryId);
    Task<AgeRating> GetMaxAgeRatingFromSeriesAsync(IEnumerable<int> seriesIds);
    Task<IList<SeriesMetadataDto>> GetSeriesMetadataForIds(IEnumerable<int> seriesIds);
    Task<IList<Series>> GetAllWithCoversInDifferentEncoding(EncodeFormat encodeFormat, bool customOnly = true);
    Task<SeriesDto?> GetSeriesDtoByNamesAndMetadataIds(IEnumerable<string> names, LibraryType libraryType, string aniListUrl, string malUrl);
    Task<int> GetAverageUserRating(int seriesId, int userId);
    Task RemoveFromOnDeck(int seriesId, int userId);
    Task ClearOnDeckRemoval(int seriesId, int userId);
    Task<PagedList<SeriesDto>> GetSeriesDtoForLibraryIdV2Async(int userId, UserParams userParams, FilterV2Dto filterDto, QueryContext queryContext = QueryContext.None);
    Task<PlusSeriesRequestDto?> GetPlusSeriesDto(int seriesId);
    Task<Series?> MatchSeries(ExternalSeriesDetailDto externalSeries);
}
