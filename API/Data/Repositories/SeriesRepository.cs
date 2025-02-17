using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using API.Constants;
using API.Data.Misc;
using API.Data.Scanner;
using API.DTOs;
using API.DTOs.Collection;
using API.DTOs.CollectionTags;
using API.DTOs.Dashboard;
using API.DTOs.Filtering;
using API.DTOs.Filtering.v2;
using API.DTOs.Metadata;
using API.DTOs.ReadingLists;
using API.DTOs.Recommendation;
using API.DTOs.Scrobbling;
using API.DTOs.Search;
using API.DTOs.SeriesDetail;
using API.DTOs.Settings;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Extensions;
using API.Extensions.QueryExtensions;
using API.Extensions.QueryExtensions.Filtering;
using API.Helpers;
using API.Helpers.Converters;
using API.Services;
using API.Services.Plus;
using API.Services.Tasks;
using API.Services.Tasks.Scanner;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;


namespace API.Data.Repositories;

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
    void Attach(Series series);
    void Attach(SeriesRelation relation);
    void Update(Series series);
    void Remove(Series series);
    void Remove(IEnumerable<Series> series);
    void Detach(Series series);
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
    /// <summary>
    /// Used to add Progress/Rating information to series list.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="series"></param>
    /// <returns></returns>
    Task AddSeriesModifiers(int userId, IList<SeriesDto> series);
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
    Task<IList<SeriesMetadata>> GetSeriesMetadataForIdsAsync(IEnumerable<int> seriesIds);
    Task<IEnumerable<GroupedSeriesDto>> GetRecentlyUpdatedSeries(int userId, int pageSize = 30);
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
    /// <summary>
    /// This is only used for <see cref="MigrateUserProgressLibraryId"/>
    /// </summary>
    /// <returns></returns>
    Task<IDictionary<int, int>> GetLibraryIdsForSeriesAsync();
    Task<IList<SeriesMetadataDto>> GetSeriesMetadataForIds(IEnumerable<int> seriesIds);
    Task<IList<Series>> GetAllWithCoversInDifferentEncoding(EncodeFormat encodeFormat, bool customOnly = true);
    Task<SeriesDto?> GetSeriesDtoByNamesAndMetadataIds(IEnumerable<string> names, LibraryType libraryType, string aniListUrl, string malUrl);
    Task<int> GetAverageUserRating(int seriesId, int userId);
    Task RemoveFromOnDeck(int seriesId, int userId);
    Task ClearOnDeckRemoval(int seriesId, int userId);
    Task<PagedList<SeriesDto>> GetSeriesDtoForLibraryIdV2Async(int userId, UserParams userParams, FilterV2Dto filterDto, QueryContext queryContext = QueryContext.None);
    Task<PlusSeriesRequestDto?> GetPlusSeriesDto(int seriesId);
    Task<int> GetCountAsync();
    Task<Series?> MatchSeries(ExternalSeriesDetailDto externalSeries);
}

public class SeriesRepository : ISeriesRepository
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;
    private readonly UserManager<AppUser> _userManager;

    private readonly Regex _yearRegex = new Regex(@"\d{4}", RegexOptions.Compiled,
        Services.Tasks.Scanner.Parser.Parser.RegexTimeout);

    public SeriesRepository(DataContext context, IMapper mapper, UserManager<AppUser> userManager)
    {
        _context = context;
        _mapper = mapper;
        _userManager = userManager;
    }

    public void Add(Series series)
    {
        _context.Series.Add(series);
    }

    public void Attach(Series series)
    {
        _context.Series.Attach(series);
    }

    public void Attach(SeriesRelation relation)
    {
        _context.SeriesRelation.Attach(relation);
    }

    public void Attach(ExternalSeriesMetadata metadata)
    {
        _context.ExternalSeriesMetadata.Attach(metadata);
    }

    public void Update(Series series)
    {
        _context.Entry(series).State = EntityState.Modified;
    }

    public void Remove(Series series)
    {
        _context.Series.Remove(series);
    }

    public void Remove(IEnumerable<Series> series)
    {
        _context.Series.RemoveRange(series);
    }

    public void Detach(Series series)
    {
        _context.Entry(series).State = EntityState.Detached;
    }

    /// <summary>
    /// Returns if a series name and format exists already in a library
    /// </summary>
    /// <param name="name">Name of series</param>
    /// <param name="libraryId"></param>
    /// <param name="format">Format of series</param>
    /// <returns></returns>
    public async Task<bool> DoesSeriesNameExistInLibrary(string name, int libraryId, MangaFormat format)
    {
        return await _context.Series
            .AsNoTracking()
            .Where(s => s.LibraryId == libraryId && s.Name.Equals(name) && s.Format == format)
            .AnyAsync();
    }


    public async Task<IEnumerable<Series>> GetSeriesForLibraryIdAsync(int libraryId, SeriesIncludes includes = SeriesIncludes.None)
    {
        return await _context.Series
            .Where(s => s.LibraryId == libraryId)
            .Includes(includes)
            .OrderBy(s => s.SortName.ToLower())
            .ToListAsync();
    }

    /// <summary>
    /// Used for <see cref="ScannerService"/> to
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="userParams"></param>
    /// <returns></returns>
    public async Task<PagedList<Series>> GetFullSeriesForLibraryIdAsync(int libraryId, UserParams userParams)
    {
        #nullable  disable
        var query = _context.Series
            .Where(s => s.LibraryId == libraryId)

            .Include(s => s.Metadata)
            .ThenInclude(m => m.CollectionTags)

            .Include(s => s.Metadata)
            .ThenInclude(m => m.People)

            .Include(s => s.Metadata)
            .ThenInclude(m => m.Genres)

            .Include(s => s.Metadata)
            .ThenInclude(m => m.Tags)

            .Include(s => s.Volumes)
            .ThenInclude(v => v.Chapters)
            .ThenInclude(cm => cm.People)

            .Include(s => s.Volumes)
            .ThenInclude(v => v.Chapters)
            .ThenInclude(c => c.Genres)

            .Include(s => s.Volumes)
            .ThenInclude(v => v.Chapters)
            .ThenInclude(c => c.Tags)

            .Include(s => s.Volumes)!
            .ThenInclude(v => v.Chapters)!
            .ThenInclude(c => c.Files)
            .AsSplitQuery()
            .OrderBy(s => s.SortName.ToLower());
#nullable  enable

        return await PagedList<Series>.CreateAsync(query, userParams.PageNumber, userParams.PageSize);
    }

    /// <summary>
    /// This is a heavy call. Returns all entities down to Files and Library and Series Metadata.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    public async Task<Series?> GetFullSeriesForSeriesIdAsync(int seriesId)
    {
        #nullable  disable
        return await _context.Series
            .Where(s => s.Id == seriesId)
            .Include(s => s.Relations)
            .Include(s => s.Metadata)
            .ThenInclude(m => m.People)
            .Include(s => s.Metadata)
            .ThenInclude(m => m.Genres)
            .Include(s => s.Library)
            .Include(s => s.Volumes)
            .ThenInclude(v => v.Chapters)
            .ThenInclude(cm => cm.People)

            .Include(s => s.Volumes)
            .ThenInclude(v => v.Chapters)
            .ThenInclude(c => c.Tags)

            .Include(s => s.Volumes)
            .ThenInclude(v => v.Chapters)
            .ThenInclude(c => c.Genres)


            .Include(s => s.Metadata)
            .ThenInclude(m => m.Tags)

            .Include(s => s.Volumes)
            .ThenInclude(v => v.Chapters)
            .ThenInclude(c => c.Files)
            .AsSplitQuery()
            .SingleOrDefaultAsync();
        #nullable  enable
    }

    /// <summary>
    /// Gets all series
    /// </summary>
    /// <param name="libraryId">Restricts to just one library</param>
    /// <param name="userId"></param>
    /// <param name="userParams"></param>
    /// <param name="filter"></param>
    /// <returns></returns>
    [Obsolete("Use GetSeriesDtoForLibraryIdAsync")]
    public async Task<PagedList<SeriesDto>> GetSeriesDtoForLibraryIdAsync(int libraryId, int userId, UserParams userParams, FilterDto filter)
    {
        var query = await CreateFilteredSearchQueryable(userId, libraryId, filter, QueryContext.None);

        var retSeries = query
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
            .AsSplitQuery()
            .AsNoTracking();

        return await PagedList<SeriesDto>.CreateAsync(retSeries, userParams.PageNumber, userParams.PageSize);
    }

    private async Task<List<int>> GetUserLibrariesForFilteredQuery(int libraryId, int userId, QueryContext queryContext)
    {
        if (libraryId == 0)
        {
            return await _context.Library.GetUserLibraries(userId, queryContext).ToListAsync();
        }

        return [libraryId];
    }

    public async Task<SearchResultGroupDto> SearchSeries(int userId, bool isAdmin, IList<int> libraryIds, string searchQuery, bool includeChapterAndFiles = true)
    {
        const int maxRecords = 15;
        var result = new SearchResultGroupDto();
        var searchQueryNormalized = searchQuery.ToNormalized();
        var userRating = await _context.AppUser.GetUserAgeRestriction(userId);

        var seriesIds = await _context.Series
            .Where(s => libraryIds.Contains(s.LibraryId))
            .RestrictAgainstAgeRestriction(userRating)
            .Select(s => s.Id)
            .ToListAsync();

        result.Libraries = await _context.Library
            .Search(searchQuery, userId, libraryIds)
            .Take(maxRecords)
            .OrderBy(l => l.Name.ToLower())
            .ProjectTo<LibraryDto>(_mapper.ConfigurationProvider)
            .ToListAsync();

        var justYear = _yearRegex.Match(searchQuery).Value;
        var hasYearInQuery = !string.IsNullOrEmpty(justYear);
        var yearComparison = hasYearInQuery ? int.Parse(justYear) : 0;

        result.Series = _context.Series
            .Where(s => libraryIds.Contains(s.LibraryId))
            .Where(s => EF.Functions.Like(s.Name, $"%{searchQuery}%")
                         || (s.OriginalName != null && EF.Functions.Like(s.OriginalName, $"%{searchQuery}%"))
                         || (s.LocalizedName != null && EF.Functions.Like(s.LocalizedName, $"%{searchQuery}%"))
                         || (EF.Functions.Like(s.NormalizedName, $"%{searchQueryNormalized}%"))
                         || (hasYearInQuery && s.Metadata.ReleaseYear == yearComparison))
            .RestrictAgainstAgeRestriction(userRating)
            .Include(s => s.Library)
            .AsNoTracking()
            .AsSplitQuery()
            .OrderBy(s => s.SortName!.ToLower())
            .Take(maxRecords)
            .ProjectTo<SearchResultDto>(_mapper.ConfigurationProvider)
            .AsEnumerable();

        result.Bookmarks = (await _context.AppUserBookmark
            .Join(
                _context.Series,
                bookmark => bookmark.SeriesId,
                series => series.Id,
                (bookmark, series) => new {Bookmark = bookmark, Series = series}
            )
            .Where(joined => joined.Bookmark.AppUserId == userId &&
                             (EF.Functions.Like(joined.Series.Name, $"%{searchQuery}%") ||
                              (joined.Series.OriginalName != null &&
                               EF.Functions.Like(joined.Series.OriginalName, $"%{searchQuery}%")) ||
                              (joined.Series.LocalizedName != null &&
                               EF.Functions.Like(joined.Series.LocalizedName, $"%{searchQuery}%"))))
            .OrderBy(joined => joined.Series.Name)
            .Take(maxRecords)
            .Select(joined => new BookmarkSearchResultDto()
            {
                SeriesName = joined.Series.Name,
                LocalizedSeriesName = joined.Series.LocalizedName,
                LibraryId = joined.Series.LibraryId,
                SeriesId = joined.Bookmark.SeriesId,
                ChapterId = joined.Bookmark.ChapterId,
                VolumeId = joined.Bookmark.VolumeId
            })
            .ToListAsync()).DistinctBy(s => s.SeriesId);


        result.ReadingLists = await _context.ReadingList
            .Search(searchQuery, userId, userRating)
            .Take(maxRecords)
            .ProjectTo<ReadingListDto>(_mapper.ConfigurationProvider)
            .ToListAsync();

        result.Collections =  await _context.AppUserCollection
            .Search(searchQuery, userId, userRating)
            .Take(maxRecords)
            .OrderBy(c => c.NormalizedTitle)
            .ProjectTo<AppUserCollectionDto>(_mapper.ConfigurationProvider)
            .ToListAsync();

        result.Persons = await _context.SeriesMetadata
            .SearchPeople(searchQuery, seriesIds)
            .Take(maxRecords)
            .OrderBy(t => t.NormalizedName)
            .Distinct()
            .ProjectTo<PersonDto>(_mapper.ConfigurationProvider)
            .ToListAsync();

        result.Genres = await _context.SeriesMetadata
            .SearchGenres(searchQuery, seriesIds)
            .Take(maxRecords)
            .ProjectTo<GenreTagDto>(_mapper.ConfigurationProvider)
            .ToListAsync();

        result.Tags = await _context.SeriesMetadata
            .SearchTags(searchQuery, seriesIds)
            .Take(maxRecords)
            .ProjectTo<TagDto>(_mapper.ConfigurationProvider)
            .ToListAsync();

        result.Files = new List<MangaFileDto>();
        result.Chapters = new List<ChapterDto>();


        if (includeChapterAndFiles)
        {
            var fileIds = _context.Series
                .Where(s => seriesIds.Contains(s.Id))
                .AsSplitQuery()
                .SelectMany(s => s.Volumes)
                .SelectMany(v => v.Chapters)
                .SelectMany(c => c.Files.Select(f => f.Id));

            // Need to check if an admin
            var user = await _context.AppUser.FirstAsync(u => u.Id == userId);
            if (await _userManager.IsInRoleAsync(user, PolicyConstants.AdminRole))
            {
                result.Files = await _context.MangaFile
                    .Where(m => EF.Functions.Like(m.FilePath, $"%{searchQuery}%") && fileIds.Contains(m.Id))
                    .AsSplitQuery()
                    .OrderBy(f => f.FilePath)
                    .Take(maxRecords)
                    .ProjectTo<MangaFileDto>(_mapper.ConfigurationProvider)
                    .ToListAsync();
            }

            result.Chapters = await _context.Chapter
                .Include(c => c.Files)
                .Where(c => EF.Functions.Like(c.TitleName, $"%{searchQuery}%")
                            || EF.Functions.Like(c.ISBN, $"%{searchQuery}%")
                            || EF.Functions.Like(c.Range, $"%{searchQuery}%")
                )
                .Where(c => c.Files.All(f => fileIds.Contains(f.Id)))
                .AsSplitQuery()
                .OrderBy(c => c.TitleName)
                .Take(maxRecords)
                .ProjectTo<ChapterDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
        }

        return result;
    }

    /// <summary>
    /// Includes Progress for the user
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<SeriesDto?> GetSeriesDtoByIdAsync(int seriesId, int userId)
    {
        var series = await _context.Series.Where(x => x.Id == seriesId)
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
            .SingleOrDefaultAsync();

        if (series == null) return null;

        var seriesList = new List<SeriesDto>() {series};
        await AddSeriesModifiers(userId, seriesList);

        return seriesList[0];
    }

    /// <summary>
    /// Returns Volumes, Metadata (Incl Genres and People), and Collection Tags
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="includes"></param>
    /// <returns></returns>
    public async Task<Series?> GetSeriesByIdAsync(int seriesId, SeriesIncludes includes = SeriesIncludes.Volumes | SeriesIncludes.Metadata)
    {
        return await _context.Series
            .Where(s => s.Id == seriesId)
            .Includes(includes)
            .SingleOrDefaultAsync();
    }

    /// <summary>
    /// Returns Full Series including all external links
    /// </summary>
    /// <param name="seriesIds"></param>
    /// <param name="fullSeries">Include all the includes or just the Series</param>
    /// <returns></returns>
    public async Task<IList<Series>> GetSeriesByIdsAsync(IList<int> seriesIds, bool fullSeries = true)
    {
        var query = _context.Series
            .Where(s => seriesIds.Contains(s.Id))
            .AsSplitQuery();

        if (!fullSeries) return await query.ToListAsync();

        return await query.Include(s => s.Volumes)
            .Include(s => s.Relations)
            .Include(s => s.Metadata)

            .Include(s => s.ExternalSeriesMetadata)

            .Include(s => s.ExternalSeriesMetadata)
            .ThenInclude(e => e.ExternalRatings)
            .Include(s => s.ExternalSeriesMetadata)
            .ThenInclude(e => e.ExternalReviews)
            .Include(s => s.ExternalSeriesMetadata)
            .ThenInclude(e => e.ExternalRecommendations)
            .ToListAsync();
    }

    public async Task<IList<SeriesDto>> GetSeriesDtoByIdsAsync(IEnumerable<int> seriesIds, AppUser user)
    {
        var allowedLibraries = await _context.Library
            .Where(library => library.AppUsers.Any(x => x.Id == user.Id))
            .Select(l => l.Id)
            .ToListAsync();
        var restriction = new AgeRestriction()
        {
            AgeRating = user.AgeRestriction,
            IncludeUnknowns = user.AgeRestrictionIncludeUnknowns
        };
        return await _context.Series
            .Include(s => s.Metadata)
            .Where(s => seriesIds.Contains(s.Id) && allowedLibraries.Contains(s.LibraryId))
            .RestrictAgainstAgeRestriction(restriction)
            .AsSplitQuery()
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<int[]> GetChapterIdsForSeriesAsync(IList<int> seriesIds)
    {
        var volumes = await _context.Volume
            .Where(v => seriesIds.Contains(v.SeriesId))
            .Include(v => v.Chapters)
            .AsSplitQuery()
            .ToListAsync();

        IList<int> chapterIds = new List<int>();
        foreach (var v in volumes)
        {
            foreach (var c in v.Chapters)
            {
                chapterIds.Add(c.Id);
            }
        }

        return chapterIds.ToArray();
    }

    /// <summary>
    /// This returns a dictionary mapping seriesId -> list of chapters back for each series id passed
    /// </summary>
    /// <param name="seriesIds"></param>
    /// <returns></returns>
    public async Task<IDictionary<int, IList<int>>> GetChapterIdWithSeriesIdForSeriesAsync(int[] seriesIds)
    {
        var volumes = await _context.Volume
            .Where(v => seriesIds.Contains(v.SeriesId))
            .Include(v => v.Chapters)
            .AsSplitQuery()
            .ToListAsync();

        var seriesChapters = new Dictionary<int, IList<int>>();
        foreach (var v in volumes)
        {
            foreach (var c in v.Chapters)
            {
                if (!seriesChapters.ContainsKey(v.SeriesId))
                {
                    var list = new List<int>();
                    seriesChapters.Add(v.SeriesId, list);
                }
                seriesChapters[v.SeriesId].Add(c.Id);
            }
        }

        return seriesChapters;
    }

    public async Task<IDictionary<int, int>> GetLibraryIdsForSeriesAsync()
    {
        var seriesChapters = new Dictionary<int, int>();
        var series = await _context.Series.Select(s => new
        {
            Id = s.Id, LibraryId = s.LibraryId
        }).ToListAsync();
        foreach (var s in series)
        {
            seriesChapters.Add(s.Id, s.LibraryId);
        }

        return seriesChapters;
    }

    public async Task<IList<SeriesMetadataDto>> GetSeriesMetadataForIds(IEnumerable<int> seriesIds)
    {
        return await _context.SeriesMetadata
            .Where(metadata => seriesIds.Contains(metadata.SeriesId))
            .Include(m => m.Genres.OrderBy(g => g.NormalizedTitle))
            .Include(m => m.Tags.OrderBy(g => g.NormalizedTitle))
            .Include(m => m.People)
            .ThenInclude(p => p.Person)
            .AsNoTracking()
            .ProjectTo<SeriesMetadataDto>(_mapper.ConfigurationProvider)
            .AsSplitQuery()
            .ToListAsync();
    }


    /// <summary>
    /// Returns custom images only
    /// </summary>
    /// <remarks>If customOnly, this will not include any volumes/chapters</remarks>
    /// <returns></returns>
    public async Task<IList<Series>> GetAllWithCoversInDifferentEncoding(EncodeFormat encodeFormat,
        bool customOnly = true)
    {
        var extension = encodeFormat.GetExtension();
        var prefix = ImageService.GetSeriesFormat(0).Replace("0", string.Empty);
        var query = _context.Series
            .Where(c => !string.IsNullOrEmpty(c.CoverImage)
                        && !c.CoverImage.EndsWith(extension)
                        && (!customOnly || c.CoverImage.StartsWith(prefix)))
            .AsSplitQuery();

        if (!customOnly)
        {
            query = query.Include(s => s.Volumes)
                .ThenInclude(v => v.Chapters);
        }

        return await query.ToListAsync();
    }

    public async Task<PagedList<SeriesDto>> GetSeriesDtoForLibraryIdV2Async(int userId, UserParams userParams, FilterV2Dto filterDto, QueryContext queryContext = QueryContext.None)
    {
        var query = await CreateFilteredSearchQueryableV2(userId, filterDto, queryContext);

        var retSeries = query
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
            .AsNoTracking();

        return await PagedList<SeriesDto>.CreateAsync(retSeries, userParams.PageNumber, userParams.PageSize);
    }

    public async Task<PlusSeriesRequestDto?> GetPlusSeriesDto(int seriesId)
    {
        return await _context.Series
            .Where(s => s.Id == seriesId)
            .Select(series => new PlusSeriesRequestDto()
            {
                MediaFormat = series.Library.Type.ConvertToPlusMediaFormat(series.Format),
                SeriesName = series.Name,
                AltSeriesName = series.LocalizedName,
                AniListId = ScrobblingService.ExtractId<int?>(series.Metadata.WebLinks,
                    ScrobblingService.AniListWeblinkWebsite),
                MalId = ScrobblingService.ExtractId<long?>(series.Metadata.WebLinks,
                    ScrobblingService.MalWeblinkWebsite),
                GoogleBooksId = ScrobblingService.ExtractId<string?>(series.Metadata.WebLinks,
                    ScrobblingService.GoogleBooksWeblinkWebsite),
                MangaDexId = ScrobblingService.ExtractId<string?>(series.Metadata.WebLinks,
                    ScrobblingService.MangaDexWeblinkWebsite),
                VolumeCount = series.Volumes.Count,
                ChapterCount = series.Volumes.SelectMany(v => v.Chapters).Count(c => !c.IsSpecial),
                Year = series.Metadata.ReleaseYear
            })
            .FirstOrDefaultAsync();
    }

    public async Task<int> GetCountAsync()
    {
        return await _context.Series.CountAsync();
    }

    public async Task AddSeriesModifiers(int userId, IList<SeriesDto> series)
    {
        var userProgress = await _context.AppUserProgresses
            .Where(p => p.AppUserId == userId && series.Select(s => s.Id).Contains(p.SeriesId))
            .AsSplitQuery()
            .ToListAsync();

        var userRatings = await _context.AppUserRating
            .Where(r => r.AppUserId == userId && series.Select(s => s.Id).Contains(r.SeriesId))
            .AsSplitQuery()
            .ToListAsync();

        foreach (var s in series)
        {
            s.PagesRead = userProgress.Where(p => p.SeriesId == s.Id).Sum(p => p.PagesRead);
            var rating = userRatings.SingleOrDefault(r => r.SeriesId == s.Id);
            if (rating != null)
            {
                s.UserRating = rating.Rating;
                s.HasUserRated = rating.HasBeenRated;
            }

            if (userProgress.Count > 0)
            {
                s.LatestReadDate = userProgress.Max(p => p.LastModified);
            }
        }
    }

    public async Task<string?> GetSeriesCoverImageAsync(int seriesId)
    {
        return await _context.Series
            .Where(s => s.Id == seriesId)
            .Select(s => s.CoverImage)
            .SingleOrDefaultAsync();
    }


    /// <summary>
    /// Returns a list of Series that were added, ordered by Created desc
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="libraryId">Library to restrict to, if 0, will apply to all libraries</param>
    /// <param name="userParams">Contains pagination information</param>
    /// <param name="filter">Optional filter on query</param>
    /// <returns></returns>
    [Obsolete("Use GetRecentlyAddedV2")]
    public async Task<PagedList<SeriesDto>> GetRecentlyAdded(int libraryId, int userId, UserParams userParams, FilterDto filter)
    {
        var query = await CreateFilteredSearchQueryable(userId, libraryId, filter, QueryContext.Dashboard);

        var retSeries = query
            .OrderByDescending(s => s.Created)
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
            .AsSplitQuery()
            .AsNoTracking();

        return await PagedList<SeriesDto>.CreateAsync(retSeries, userParams.PageNumber, userParams.PageSize);
    }

    public async Task<PagedList<SeriesDto>> GetRecentlyAddedV2(int userId, UserParams userParams, FilterV2Dto filter)
    {
        var query = await CreateFilteredSearchQueryableV2(userId, filter, QueryContext.Dashboard);

        var retSeries = query
            .OrderByDescending(s => s.Created)
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
            .AsSplitQuery()
            .AsNoTracking();

        return await PagedList<SeriesDto>.CreateAsync(retSeries, userParams.PageNumber, userParams.PageSize);
    }

    private IList<MangaFormat> ExtractFilters(int libraryId, int userId, FilterDto filter, ref List<int> userLibraries,
        out List<int> allPeopleIds, out bool hasPeopleFilter, out bool hasGenresFilter, out bool hasCollectionTagFilter,
        out bool hasRatingFilter, out bool hasProgressFilter, out IList<int> seriesIds, out bool hasAgeRating, out bool hasTagsFilter,
        out bool hasLanguageFilter, out bool hasPublicationFilter, out bool hasSeriesNameFilter, out bool hasReleaseYearMinFilter, out bool hasReleaseYearMaxFilter)
    {
        var formats = filter.GetSqlFilter();

        if (filter.Libraries.Count > 0)
        {
            userLibraries = userLibraries.Where(l => filter.Libraries.Contains(l)).ToList();
        }
        else if (libraryId > 0)
        {
            userLibraries = userLibraries.Where(l => l == libraryId).ToList();
        }

        allPeopleIds = new List<int>();
        allPeopleIds.AddRange(filter.Writers);
        allPeopleIds.AddRange(filter.Character);
        allPeopleIds.AddRange(filter.Colorist);
        allPeopleIds.AddRange(filter.Editor);
        allPeopleIds.AddRange(filter.Inker);
        allPeopleIds.AddRange(filter.Letterer);
        allPeopleIds.AddRange(filter.Penciller);
        allPeopleIds.AddRange(filter.Publisher);
        allPeopleIds.AddRange(filter.CoverArtist);
        allPeopleIds.AddRange(filter.Translators);

        hasPeopleFilter = allPeopleIds.Count > 0;
        hasGenresFilter = filter.Genres.Count > 0;
        hasCollectionTagFilter = filter.CollectionTags.Count > 0;
        hasRatingFilter = filter.Rating > 0;
        hasProgressFilter = !filter.ReadStatus.Read || !filter.ReadStatus.InProgress || !filter.ReadStatus.NotRead;
        hasAgeRating = filter.AgeRating.Count > 0;
        hasTagsFilter = filter.Tags.Count > 0;
        hasLanguageFilter = filter.Languages.Count > 0;
        hasPublicationFilter = filter.PublicationStatus.Count > 0;

        hasReleaseYearMinFilter = filter.ReleaseYearRange != null && filter.ReleaseYearRange.Min != 0;
        hasReleaseYearMaxFilter = filter.ReleaseYearRange != null && filter.ReleaseYearRange.Max != 0;


        bool ProgressComparison(int pagesRead, int totalPages)
        {
            var result = false;
            if (filter.ReadStatus.NotRead)
            {
                result = (pagesRead == 0);
            }

            if (filter.ReadStatus.Read)
            {
                result = result || (pagesRead == totalPages);
            }

            if (filter.ReadStatus.InProgress)
            {
                result = result || (pagesRead > 0 && pagesRead < totalPages);
            }

            return result;
        }

        seriesIds = new List<int>();
        if (hasProgressFilter)
        {
            seriesIds = _context.Series
                .Include(s => s.Progress)
                .Select(s => new
                {
                    Series = s,
                    PagesRead = s.Progress.Where(p => p.AppUserId == userId).Sum(p => p.PagesRead),
                })
                .AsEnumerable()
                .Where(s => ProgressComparison(s.PagesRead, s.Series.Pages))
                .Select(s => s.Series.Id)
                .ToList();
        }

        hasSeriesNameFilter = !string.IsNullOrEmpty(filter.SeriesNameQuery);

        return formats;
    }

    /// <summary>
    /// Returns Series that the user has some partial progress on. Sorts based on activity. Sort first by User progress, then
    /// by when chapters have been added to series. Restricts progress in the past 30 days and chapters being added to last 7.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="libraryId">Library to restrict to, if 0, will apply to all libraries</param>
    /// <param name="userParams">Pagination information</param>
    /// <param name="filter">Optional (default null) filter on query</param>
    /// <returns></returns>
    public async Task<PagedList<SeriesDto>> GetOnDeck(int userId, int libraryId, UserParams userParams, FilterDto? filter)
    {
        var settings = await _context.ServerSetting
            .Select(x => x)
            .AsNoTracking()
            .ToListAsync();
        var serverSettings = _mapper.Map<ServerSettingDto>(settings);

        var cutoffProgressPoint = DateTime.Now - TimeSpan.FromDays(serverSettings.OnDeckProgressDays);
        var cutoffLastAddedPoint = DateTime.Now - TimeSpan.FromDays(serverSettings.OnDeckUpdateDays);

        var libraryIds = GetLibraryIdsForUser(userId, libraryId, QueryContext.Dashboard)
            .Where(id => libraryId == 0 || id == libraryId);
        var usersSeriesIds = GetSeriesIdsForLibraryIds(libraryIds);

        // Don't allow any series the user has explicitly removed
        var onDeckRemovals = _context.AppUserOnDeckRemoval
            .Where(d => d.AppUserId == userId)
            .Select(d => d.SeriesId)
            .AsEnumerable();

        var query = _context.Series
            .Where(s => usersSeriesIds.Contains(s.Id))
            .Where(s => !onDeckRemovals.Contains(s.Id))
            .Select(s => new
            {
                Series = s,
                PagesRead = _context.AppUserProgresses.Where(p => p.SeriesId == s.Id && p.AppUserId == userId)
                    .Sum(s1 => s1.PagesRead),
                LatestReadDate = _context.AppUserProgresses
                    .Where(p => p.SeriesId == s.Id && p.AppUserId == userId)
                    .Max(p => p.LastModified),
                s.LastChapterAdded,
            })
            .Where(s => s.PagesRead > 0
                        && s.PagesRead < s.Series.Pages)
            .Where(d => d.LatestReadDate >= cutoffProgressPoint || d.LastChapterAdded >= cutoffLastAddedPoint)
                .OrderByDescending(s => s.LatestReadDate)
            .ThenByDescending(s => s.LastChapterAdded)
            .Select(s => s.Series)
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
            .AsSplitQuery()
            .AsNoTracking();

        return await PagedList<SeriesDto>.CreateAsync(query, userParams.PageNumber, userParams.PageSize);
    }

    private async Task<IQueryable<Series>> CreateFilteredSearchQueryable(int userId, int libraryId, FilterDto filter, QueryContext queryContext)
    {
        // NOTE: Why do we even have libraryId when the filter has the actual libraryIds?
        var userLibraries = await GetUserLibrariesForFilteredQuery(libraryId, userId, queryContext);
        var userRating = await _context.AppUser.GetUserAgeRestriction(userId);
        var onlyParentSeries = await _context.AppUserPreferences.Where(u => u.AppUserId == userId)
            .Select(u => u.CollapseSeriesRelationships)
            .SingleOrDefaultAsync();

        var formats = ExtractFilters(libraryId, userId, filter, ref userLibraries,
            out var allPeopleIds, out var hasPeopleFilter, out var hasGenresFilter,
            out var hasCollectionTagFilter, out var hasRatingFilter, out var hasProgressFilter,
            out var seriesIds, out var hasAgeRating, out var hasTagsFilter, out var hasLanguageFilter,
            out var hasPublicationFilter, out var hasSeriesNameFilter, out var hasReleaseYearMinFilter, out var hasReleaseYearMaxFilter);

        IList<int> collectionSeries = [];
        if (hasCollectionTagFilter)
        {
            collectionSeries = await _context.AppUserCollection
                .Where(uc => uc.Promoted || uc.AppUserId == userId)
                .Where(uc => filter.CollectionTags.Contains(uc.Id))
                .SelectMany(uc => uc.Items)
                .RestrictAgainstAgeRestriction(userRating)
                .Select(s => s.Id)
                .Distinct()
                .ToListAsync();
        }


        var query = _context.Series
            .AsNoTracking()
            // This new style can handle any filterComparision coming from the user
            .HasLanguage(hasLanguageFilter, FilterComparison.Contains, filter.Languages)
            .HasReleaseYear(hasReleaseYearMaxFilter, FilterComparison.LessThanEqual, filter.ReleaseYearRange?.Max)
            .HasReleaseYear(hasReleaseYearMinFilter, FilterComparison.GreaterThanEqual, filter.ReleaseYearRange?.Min)
            .HasName(hasSeriesNameFilter, FilterComparison.Matches, filter.SeriesNameQuery)
            .HasRating(hasRatingFilter, FilterComparison.GreaterThanEqual, filter.Rating / 100f, userId)
            .HasAgeRating(hasAgeRating, FilterComparison.Contains, filter.AgeRating)
            .HasPublicationStatus(hasPublicationFilter, FilterComparison.Contains, filter.PublicationStatus)
            .HasTags(hasTagsFilter, FilterComparison.Contains, filter.Tags)
            .HasCollectionTags(hasCollectionTagFilter, FilterComparison.Contains, filter.Tags, collectionSeries)
            .HasGenre(hasGenresFilter, FilterComparison.Contains, filter.Genres)
            .HasFormat(filter.Formats != null && filter.Formats.Count > 0, FilterComparison.Contains, filter.Formats!)
            .HasAverageReadTime(true, FilterComparison.GreaterThanEqual, 0)
            .HasPeopleLegacy(hasPeopleFilter, FilterComparison.Contains, allPeopleIds)

            .WhereIf(onlyParentSeries,
                s => s.RelationOf.Count == 0 || s.RelationOf.All(p => p.RelationKind == RelationKind.Prequel))
            .Where(s => userLibraries.Contains(s.LibraryId));

        if (filter.ReadStatus.InProgress)
        {
            query = query.HasReadingProgress(hasProgressFilter, FilterComparison.GreaterThan,
                0, userId)
                .HasReadingProgress(hasProgressFilter, FilterComparison.LessThan,
                    100, userId);
        } else if (filter.ReadStatus.Read)
        {
            query = query.HasReadingProgress(hasProgressFilter, FilterComparison.Equal,
                100, userId);
        }
        else if (filter.ReadStatus.NotRead)
        {
            query = query.HasReadingProgress(hasProgressFilter, FilterComparison.Equal,
                0, userId);
        }

        if (userRating.AgeRating != AgeRating.NotApplicable)
        {
             // this if statement is included in the extension
            query = query.RestrictAgainstAgeRestriction(userRating);
        }


        // If no sort options, default to using SortName
        filter.SortOptions ??= new SortOptions()
        {
            IsAscending = true,
            SortField = SortField.SortName
        };

        query = filter.SortOptions.SortField switch
        {
            SortField.SortName => query.DoOrderBy(s => s.SortName.ToLower(), filter.SortOptions),
            SortField.CreatedDate => query.DoOrderBy(s => s.Created, filter.SortOptions),
            SortField.LastModifiedDate => query.DoOrderBy(s => s.LastModified, filter.SortOptions),
            SortField.LastChapterAdded => query.DoOrderBy(s => s.LastChapterAdded, filter.SortOptions),
            SortField.TimeToRead => query.DoOrderBy(s => s.AvgHoursToRead, filter.SortOptions),
            SortField.ReleaseYear => query.DoOrderBy(s => s.Metadata.ReleaseYear, filter.SortOptions),
            SortField.ReadProgress => query.DoOrderBy(s => s.Progress.Where(p => p.SeriesId == s.Id).Select(p => p.LastModified).Max(), filter.SortOptions),
            SortField.AverageRating => query.DoOrderBy(s => s.ExternalSeriesMetadata.ExternalRatings
                .Where(p => p.SeriesId == s.Id).Average(p => p.AverageScore), filter.SortOptions),
            _ => query
        };

        return query.AsSplitQuery();
    }

    private async Task<IQueryable<Series>> CreateFilteredSearchQueryableV2(int userId, FilterV2Dto filter, QueryContext queryContext, IQueryable<Series>? query = null)
    {
        var userLibraries = await GetUserLibrariesForFilteredQuery(0, userId, queryContext);
        var userRating = await _context.AppUser.GetUserAgeRestriction(userId);
        var onlyParentSeries = await _context.AppUserPreferences.Where(u => u.AppUserId == userId)
            .Select(u => u.CollapseSeriesRelationships)
            .SingleOrDefaultAsync();

        query ??= _context.Series
            .AsNoTracking();

        // When the user has no access, just return instantly
        if (userLibraries.Count == 0)
        {
            return query.Where(s => false);
        }



        // First setup any FilterField.Libraries in the statements, as these don't have any traditional query statements applied here
        query = ApplyLibraryFilter(filter, query);

        query = ApplyWantToReadFilter(filter, query, userId);

        query = await ApplyCollectionFilter(filter, query, userId, userRating);




        query = BuildFilterQuery(userId, filter, query);

        query = query
            .WhereIf(userLibraries.Count > 0, s => userLibraries.Contains(s.LibraryId))
            .WhereIf(onlyParentSeries, s =>
                s.RelationOf.Count == 0 ||
                s.RelationOf.All(p => p.RelationKind == RelationKind.Prequel))
            .RestrictAgainstAgeRestriction(userRating);


        return ApplyLimit(query
            .Sort(userId, filter.SortOptions)
            .AsSplitQuery()
            , filter.LimitTo);
    }

    private async Task<IQueryable<Series>> ApplyCollectionFilter(FilterV2Dto filter, IQueryable<Series> query, int userId, AgeRestriction userRating)
    {
        var collectionStmt = filter.Statements.FirstOrDefault(stmt => stmt.Field == FilterField.CollectionTags);
        if (collectionStmt == null) return query;

        var value = (IList<int>) FilterFieldValueConverter.ConvertValue(collectionStmt.Field, collectionStmt.Value);

        if (value.Count == 0)
        {
            return query;
        }

        var collectionSeries = await _context.AppUserCollection
            .Where(uc => uc.Promoted || uc.AppUserId == userId)
            .Where(uc => value.Contains(uc.Id))
            .SelectMany(uc => uc.Items)
            .RestrictAgainstAgeRestriction(userRating)
            .Select(s => s.Id)
            .Distinct()
            .ToListAsync();

        if (collectionStmt.Comparison != FilterComparison.MustContains)
            return query.HasCollectionTags(true, collectionStmt.Comparison, value, collectionSeries);

        var collectionSeriesTasks = value.Select(async collectionId =>
        {
            return await _context.AppUserCollection
                .Where(uc => uc.Promoted || uc.AppUserId == userId)
                .Where(uc => uc.Id == collectionId)
                .SelectMany(uc => uc.Items)
                .RestrictAgainstAgeRestriction(userRating)
                .Select(s => s.Id)
                .ToListAsync();
        });

        var collectionSeriesLists = await Task.WhenAll(collectionSeriesTasks);

        // Find the common series among all collections
        var commonSeries = collectionSeriesLists.Aggregate((common, next) => common.Intersect(next).ToList());

        // Filter the original query based on the common series
        return query.Where(s => commonSeries.Contains(s.Id));
    }

    private IQueryable<Series> ApplyWantToReadFilter(FilterV2Dto filter, IQueryable<Series> query, int userId)
    {
        var wantToReadStmt = filter.Statements.FirstOrDefault(stmt => stmt.Field == FilterField.WantToRead);
        if (wantToReadStmt == null) return query;

        var seriesIds = _context.AppUser.Where(u => u.Id == userId)
            .SelectMany(u => u.WantToRead)
            .Select(s => s.SeriesId);

        if (bool.Parse(wantToReadStmt.Value))
        {
            query = query.Where(s => seriesIds.Contains(s.Id));
        }
        else
        {
            query = query.Where(s => !seriesIds.Contains(s.Id));
        }

        return query;
    }

    private static IQueryable<Series> ApplyLibraryFilter(FilterV2Dto filter, IQueryable<Series> query)
    {
        var filterIncludeLibs = new List<int>();
        var filterExcludeLibs = new List<int>();

        if (filter.Statements != null)
        {
            foreach (var stmt in filter.Statements.Where(stmt => stmt.Field == FilterField.Libraries))
            {
                var libIds = stmt.Value.Split(',').Select(int.Parse);
                if (stmt.Comparison is FilterComparison.Equal or FilterComparison.Contains)
                {

                    filterIncludeLibs.AddRange(libIds);
                }
                else
                {
                    filterExcludeLibs.AddRange(libIds);
                }
            }

            // Remove as filterLibs now has everything
            filter.Statements = filter.Statements.Where(stmt => stmt.Field != FilterField.Libraries).ToList();
        }

        // We now have a list of libraries the user wants it restricted to and libraries the user doesn't want in the list
        // We need to check what the filer combo is to see how to next approach

        if (filter.Combination == FilterCombination.And)
        {
            // If the filter combo is AND, then we need 2 different queries
            query = query
                .WhereIf(filterIncludeLibs.Count > 0, s => filterIncludeLibs.Contains(s.LibraryId))
                .WhereIf(filterExcludeLibs.Count > 0, s => !filterExcludeLibs.Contains(s.LibraryId));
        }
        else
        {
            // This is an OR statement. In that case we can just remove the filterExcludes
            query = query.WhereIf(filterIncludeLibs.Count > 0, s => filterIncludeLibs.Contains(s.LibraryId));
        }

        return query;
    }

    private static IQueryable<Series> BuildFilterQuery(int userId, FilterV2Dto filterDto, IQueryable<Series> query)
    {
        if (filterDto.Statements == null || filterDto.Statements.Count == 0) return query;


        var queries = filterDto.Statements
            .Select(statement => BuildFilterGroup(userId, statement, query))
            .ToList();

        return filterDto.Combination == FilterCombination.And
            ? queries.Aggregate((q1, q2) => q1.Intersect(q2))
            : queries.Aggregate((q1, q2) => q1.Union(q2));
    }

    private static IQueryable<Series> ApplyLimit(IQueryable<Series> query, int limit)
    {
        return limit <= 0 ? query : query.Take(limit);
    }

    private static IQueryable<Series> BuildFilterGroup(int userId, FilterStatementDto statement, IQueryable<Series> query)
    {

        var value = FilterFieldValueConverter.ConvertValue(statement.Field, statement.Value);
        return statement.Field switch
        {
            FilterField.Summary => query.HasSummary(true, statement.Comparison, (string) value),
            FilterField.SeriesName => query.HasName(true, statement.Comparison, (string) value),
            FilterField.Path => query.HasPath(true, statement.Comparison, (string) value),
            FilterField.FilePath => query.HasFilePath(true, statement.Comparison, (string) value),
            FilterField.PublicationStatus => query.HasPublicationStatus(true, statement.Comparison,
                (IList<PublicationStatus>) value),
            FilterField.Languages => query.HasLanguage(true, statement.Comparison, (IList<string>) value),
            FilterField.AgeRating => query.HasAgeRating(true, statement.Comparison, (IList<AgeRating>) value),
            FilterField.UserRating => query.HasRating(true, statement.Comparison, (float) value , userId),
            FilterField.Tags => query.HasTags(true, statement.Comparison, (IList<int>) value),
            FilterField.Translators => query.HasPeople(true, statement.Comparison, (IList<int>) value, PersonRole.Translator),
            FilterField.Characters => query.HasPeople(true, statement.Comparison, (IList<int>) value, PersonRole.Character),
            FilterField.Publisher => query.HasPeople(true, statement.Comparison, (IList<int>) value, PersonRole.Publisher),
            FilterField.Editor => query.HasPeople(true, statement.Comparison, (IList<int>) value, PersonRole.Editor),
            FilterField.CoverArtist => query.HasPeople(true, statement.Comparison, (IList<int>) value, PersonRole.CoverArtist),
            FilterField.Letterer => query.HasPeople(true, statement.Comparison, (IList<int>) value, PersonRole.Letterer),
            FilterField.Colorist => query.HasPeople(true, statement.Comparison, (IList<int>) value, PersonRole.Inker),
            FilterField.Inker => query.HasPeople(true, statement.Comparison, (IList<int>) value, PersonRole.Inker),
            FilterField.Imprint => query.HasPeople(true, statement.Comparison, (IList<int>) value, PersonRole.Imprint),
            FilterField.Team => query.HasPeople(true, statement.Comparison, (IList<int>) value, PersonRole.Team),
            FilterField.Location => query.HasPeople(true, statement.Comparison, (IList<int>) value, PersonRole.Location),
            FilterField.Penciller => query.HasPeople(true, statement.Comparison, (IList<int>) value, PersonRole.Penciller),
            FilterField.Writers => query.HasPeople(true, statement.Comparison, (IList<int>) value, PersonRole.Writer),
            FilterField.Genres => query.HasGenre(true, statement.Comparison, (IList<int>) value),
            FilterField.CollectionTags =>
                // This is handled in the code before this as it's handled in a more general, combined manner
                query,
            FilterField.Libraries =>
                // This is handled in the code before this as it's handled in a more general, combined manner
                query,
            FilterField.WantToRead =>
                // This is handled in the higher level of code as it's more general
                query,
            FilterField.ReadProgress => query.HasReadingProgress(true, statement.Comparison, (float) value, userId),
            FilterField.Formats => query.HasFormat(true, statement.Comparison, (IList<MangaFormat>) value),
            FilterField.ReleaseYear => query.HasReleaseYear(true, statement.Comparison, (int) value),
            FilterField.ReadTime => query.HasAverageReadTime(true, statement.Comparison, (int) value),
            FilterField.ReadingDate => query.HasReadingDate(true, statement.Comparison, (DateTime) value, userId),
            FilterField.ReadLast => query.HasReadLast(true, statement.Comparison, (int) value, userId),
            FilterField.AverageRating => query.HasAverageRating(true, statement.Comparison, (float) value),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private async Task<IQueryable<Series>> CreateFilteredSearchQueryable(int userId, int libraryId, FilterDto filter, IQueryable<Series> sQuery)
    {
        var userLibraries = await GetUserLibrariesForFilteredQuery(libraryId, userId, QueryContext.Search);
        var formats = ExtractFilters(libraryId, userId, filter, ref userLibraries,
            out var allPeopleIds, out var hasPeopleFilter, out var hasGenresFilter,
            out var hasCollectionTagFilter, out var hasRatingFilter, out var hasProgressFilter,
            out var seriesIds, out var hasAgeRating, out var hasTagsFilter, out var hasLanguageFilter,
            out var hasPublicationFilter, out var hasSeriesNameFilter, out var hasReleaseYearMinFilter, out var hasReleaseYearMaxFilter);

        var query = sQuery
            .WhereIf(hasGenresFilter, s => s.Metadata.Genres.Any(g => filter.Genres.Contains(g.Id)))
            .WhereIf(hasPeopleFilter, s => s.Metadata.People.Any(p => allPeopleIds.Contains(p.PersonId)))
            .WhereIf(hasCollectionTagFilter,
                s => s.Metadata.CollectionTags.Any(t => filter.CollectionTags.Contains(t.Id)))
            .WhereIf(hasRatingFilter, s => s.Ratings.Any(r => r.Rating >= filter.Rating && r.AppUserId == userId))
            .WhereIf(hasProgressFilter, s => seriesIds.Contains(s.Id))
            .WhereIf(hasAgeRating, s => filter.AgeRating.Contains(s.Metadata.AgeRating))
            .WhereIf(hasTagsFilter, s => s.Metadata.Tags.Any(t => filter.Tags.Contains(t.Id)))
            .WhereIf(hasLanguageFilter, s => filter.Languages.Contains(s.Metadata.Language))
            .WhereIf(hasReleaseYearMinFilter, s => s.Metadata.ReleaseYear >= filter.ReleaseYearRange!.Min)
            .WhereIf(hasReleaseYearMaxFilter, s => s.Metadata.ReleaseYear <= filter.ReleaseYearRange!.Max)
            .WhereIf(hasPublicationFilter, s => filter.PublicationStatus.Contains(s.Metadata.PublicationStatus))
            .WhereIf(hasSeriesNameFilter, s => EF.Functions.Like(s.Name, $"%{filter.SeriesNameQuery}%")
                                               || EF.Functions.Like(s.OriginalName!, $"%{filter.SeriesNameQuery}%")
                                               || EF.Functions.Like(s.LocalizedName!, $"%{filter.SeriesNameQuery}%"))
            .Where(s => userLibraries.Contains(s.LibraryId)
                        && formats.Contains(s.Format))
            .Sort(userId, filter.SortOptions)
            .AsNoTracking();

        return query.AsSplitQuery();
    }

    public async Task<SeriesMetadataDto?> GetSeriesMetadata(int seriesId)
    {
        return await _context.SeriesMetadata
            .Where(metadata => metadata.SeriesId == seriesId)
            .Include(m => m.Genres.OrderBy(g => g.NormalizedTitle))
            .Include(m => m.Tags.OrderBy(g => g.NormalizedTitle))
            .Include(m => m.People)
            .ThenInclude(p => p.Person)
            .AsNoTracking()
            .ProjectTo<SeriesMetadataDto>(_mapper.ConfigurationProvider)
            .AsSplitQuery()
            .SingleOrDefaultAsync();
    }

    public async Task<PagedList<SeriesDto>> GetSeriesDtoForCollectionAsync(int collectionId, int userId, UserParams userParams)
    {
        var userLibraries = _context.Library.GetUserLibraries(userId);

        var query =  _context.AppUserCollection
            .Where(s => s.Id == collectionId)
            .Include(c => c.Items)
            .SelectMany(c => c.Items.Where(s => userLibraries.Contains(s.LibraryId)))
            .OrderBy(s => s.LibraryId)
            .ThenBy(s => s.SortName.ToLower())
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
            .AsSplitQuery();

        return await PagedList<SeriesDto>.CreateAsync(query, userParams.PageNumber, userParams.PageSize);
    }

    public async Task<IList<MangaFile>> GetFilesForSeries(int seriesId)
    {
        return await _context.Volume
            .Where(v => v.SeriesId == seriesId)
            .Include(v => v.Chapters)
            .ThenInclude(c => c.Files)
            .SelectMany(v => v.Chapters.SelectMany(c => c.Files))
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<SeriesDto>> GetSeriesDtoForIdsAsync(IEnumerable<int> seriesIds, int userId)
    {
        var allowedLibraries = _context.Library
            .Include(l => l.AppUsers)
            .Where(library => library.AppUsers.Any(x => x.Id == userId))
            .AsSplitQuery()
            .Select(l => l.Id);
        var userRating = await _context.AppUser.GetUserAgeRestriction(userId);

        return await _context.Series
            .RestrictAgainstAgeRestriction(userRating)
            .Where(s => seriesIds.Contains(s.Id) && allowedLibraries.Contains(s.LibraryId))
            .OrderBy(s => s.SortName.ToLower())
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
            .AsNoTracking()
            .AsSplitQuery()
            .ToListAsync();
    }

    public async Task<IList<string>> GetAllCoverImagesAsync()
    {
        return (await _context.Series
            .Select(s => s.CoverImage)
            .Where(t => !string.IsNullOrEmpty(t))
            .ToListAsync())!;
    }

    public async Task<IEnumerable<string>> GetLockedCoverImagesAsync()
    {
        return (await _context.Series
            .Where(s => s.CoverImageLocked && !string.IsNullOrEmpty(s.CoverImage))
            .Select(s => s.CoverImage)
            .ToListAsync())!;
    }

    /// <summary>
    /// Returns the number of series for a given library (or all libraries if libraryId is 0)
    /// </summary>
    /// <param name="libraryId">Defaults to 0, library to restrict count to</param>
    /// <returns></returns>
    private async Task<int> GetSeriesCount(int libraryId = 0)
    {
        if (libraryId > 0)
        {
            return await _context.Series
                .Where(s => s.LibraryId == libraryId)
                .CountAsync();
        }
        return await _context.Series.CountAsync();
    }

    /// <summary>
    /// Returns the number of series that should be processed in parallel to optimize speed and memory. Minimum of 50
    /// </summary>
    /// <param name="libraryId">Defaults to 0 meaning no library</param>
    /// <returns></returns>
    private async Task<Tuple<int, int>> GetChunkSize(int libraryId = 0)
    {
        var totalSeries = await GetSeriesCount(libraryId);
        return new Tuple<int, int>(totalSeries, 50);
    }

    public async Task<Chunk> GetChunkInfo(int libraryId = 0)
    {
        var (totalSeries, chunkSize) = await GetChunkSize(libraryId);

        if (totalSeries == 0) return new Chunk()
        {
            TotalChunks = 0,
            TotalSize = 0,
            ChunkSize = 0
        };

        var totalChunks = Math.Max((int) Math.Ceiling((totalSeries * 1.0) / chunkSize), 1);

        return new Chunk()
        {
            TotalSize = totalSeries,
            ChunkSize = chunkSize,
            TotalChunks = totalChunks
        };
    }

    public async Task<IList<SeriesMetadata>> GetSeriesMetadataForIdsAsync(IEnumerable<int> seriesIds)
    {
        return await _context.SeriesMetadata
            .Where(sm => seriesIds.Contains(sm.SeriesId))
            .Include(sm => sm.CollectionTags)
            .AsSplitQuery()
            .ToListAsync();
    }




    /// <summary>
    /// Return recently updated series, regardless of read progress, and group the number of volume or chapters added.
    /// </summary>
    /// <remarks>This provides 2 levels of pagination. Fetching the individual chapters only looks at 3000. Then when performing grouping
    /// in memory, we stop after 30 series. </remarks>
    /// <param name="userId">Used to ensure user has access to libraries</param>
    /// <param name="pageSize">How many entities to return</param>
    /// <returns></returns>
    public async Task<IEnumerable<GroupedSeriesDto>> GetRecentlyUpdatedSeries(int userId, int pageSize = 30)
    {
        var seriesMap = new Dictionary<string, GroupedSeriesDto>();
        var index = 0;
        var userRating = await _context.AppUser.GetUserAgeRestriction(userId);

        var items = (await GetRecentlyAddedChaptersQuery(userId));
        if (userRating.AgeRating != AgeRating.NotApplicable)
        {
            items = items.RestrictAgainstAgeRestriction(userRating);
        }

        foreach (var item in items)
        {
            if (seriesMap.Keys.Count == pageSize) break;

            if (item.SeriesName == null) continue;


            if (seriesMap.TryGetValue(item.SeriesName + "_" + item.LibraryId, out var value))
            {
                value.Count += 1;
            }
            else
            {
                seriesMap[item.SeriesName + "_" + item.LibraryId] = new GroupedSeriesDto()
                {
                    LibraryId = item.LibraryId,
                    LibraryType = item.LibraryType,
                    SeriesId = item.SeriesId,
                    SeriesName = item.SeriesName,
                    Created = item.Created,
                    Id = index,
                    Format = item.Format,
                    Count = 1,
                };
                index += 1;
            }
        }

        return seriesMap.Values.AsEnumerable();
    }

    public async Task<IEnumerable<SeriesDto>> GetSeriesForRelationKind(int userId, int seriesId, RelationKind kind)
    {
        var libraryIds = GetLibraryIdsForUser(userId);
        var userRating = await _context.AppUser.GetUserAgeRestriction(userId);

        var usersSeriesIds = _context.Series
            .Where(s => libraryIds.Contains(s.LibraryId))
            .RestrictAgainstAgeRestriction(userRating)
            .Select(s => s.Id);

        var targetSeries = _context.SeriesRelation
            .Where(sr =>
                sr.SeriesId == seriesId && sr.RelationKind == kind && usersSeriesIds.Contains(sr.TargetSeriesId))
            .Include(sr => sr.TargetSeries)
            .AsSplitQuery()
            .AsNoTracking()
            .Select(sr => sr.TargetSeriesId);

        return await _context.Series
            .Where(s => targetSeries.Contains(s.Id))
            .RestrictAgainstAgeRestriction(userRating)
            .AsSplitQuery()
            .AsNoTracking()
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<PagedList<SeriesDto>> GetMoreIn(int userId, int libraryId, int genreId, UserParams userParams)
    {
        var libraryIds = GetLibraryIdsForUser(userId, libraryId, QueryContext.Dashboard)
            .Where(id => libraryId == 0 || id == libraryId);
        var usersSeriesIds = GetSeriesIdsForLibraryIds(libraryIds);

        var userRating = await _context.AppUser.GetUserAgeRestriction(userId);
        // Because this can be called from an API, we need to provide an additional check if the genre has anything the
        // user with age restrictions can access

        var query = _context.Series
            .Where(s => s.Metadata.Genres.Select(g => g.Id).Contains(genreId))
            .Where(s => usersSeriesIds.Contains(s.Id))
            .RestrictAgainstAgeRestriction(userRating)
            .AsSplitQuery()
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider);


        return await PagedList<SeriesDto>.CreateAsync(query, userParams.PageNumber, userParams.PageSize);
    }

    /// <summary>
    /// Returns a list of Series that the user Has fully read
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="libraryId"></param>
    /// <param name="userParams"></param>
    /// <returns></returns>
    public async Task<PagedList<SeriesDto>> GetRediscover(int userId, int libraryId, UserParams userParams)
    {
        var libraryIds = GetLibraryIdsForUser(userId, libraryId, QueryContext.Recommended)
            .Where(id => libraryId == 0 || id == libraryId);
        var usersSeriesIds = GetSeriesIdsForLibraryIds(libraryIds);
        var distinctSeriesIdsWithProgress = _context.AppUserProgresses
            .Where(s => usersSeriesIds.Contains(s.SeriesId))
            .Select(p => p.SeriesId)
            .Distinct();

        var query = _context.Series
            .Where(s => distinctSeriesIdsWithProgress.Contains(s.Id) &&
                        _context.AppUserProgresses.Where(s1 => s1.SeriesId == s.Id && s1.AppUserId == userId)
                            .Sum(s1 => s1.PagesRead) >= s.Pages)
            .AsSplitQuery()
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider);

        return await PagedList<SeriesDto>.CreateAsync(query, userParams.PageNumber, userParams.PageSize);
    }

    public async Task<SeriesDto?> GetSeriesForMangaFile(int mangaFileId, int userId)
    {
        var libraryIds = GetLibraryIdsForUser(userId, 0, QueryContext.Search);
        var userRating = await _context.AppUser.GetUserAgeRestriction(userId);

        return await _context.MangaFile
            .Where(m => m.Id == mangaFileId)
            .AsSplitQuery()
            .Select(f => f.Chapter)
            .Select(c => c.Volume)
            .Select(v => v.Series)
            .Where(s => libraryIds.Contains(s.LibraryId))
            .RestrictAgainstAgeRestriction(userRating)
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
            .SingleOrDefaultAsync();
    }

    public async Task<SeriesDto?> GetSeriesForChapter(int chapterId, int userId)
    {
        var libraryIds = GetLibraryIdsForUser(userId);
        var userRating = await _context.AppUser.GetUserAgeRestriction(userId);
        return await _context.Chapter
            .Where(m => m.Id == chapterId)
            .AsSplitQuery()
            .Select(c => c.Volume)
            .Select(v => v.Series)
            .Where(s => libraryIds.Contains(s.LibraryId))
            .RestrictAgainstAgeRestriction(userRating)
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
            .SingleOrDefaultAsync();
    }

    /// <summary>
    /// Return a Series by Folder path. Null if not found.
    /// </summary>
    /// <param name="folder">This will be normalized in the query and checked against FolderPath and LowestFolderPath</param>
    /// <param name="includes">Additional relationships to include with the base query</param>
    /// <returns></returns>
    public async Task<Series?> GetSeriesByFolderPath(string folder, SeriesIncludes includes = SeriesIncludes.None)
    {
        var normalized = Services.Tasks.Scanner.Parser.Parser.NormalizePath(folder);
        if (string.IsNullOrEmpty(normalized)) return null;

        return await _context.Series
            .Where(s => (!string.IsNullOrEmpty(s.FolderPath) && s.FolderPath.Equals(normalized) || (!string.IsNullOrEmpty(s.LowestFolderPath) && s.LowestFolderPath.Equals(normalized))))
            .Includes(includes)
            .SingleOrDefaultAsync();
    }

    public async Task<Series?> GetSeriesThatContainsLowestFolderPath(string path, SeriesIncludes includes = SeriesIncludes.None)
    {
        // Check if the path ends with a file (has a file extension)
        string directoryPath;
        if (Path.HasExtension(path))
        {
            // Remove the file part and get the directory path
            directoryPath = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directoryPath)) return null;
        }
        else
        {
            // Use the path as is if it doesn't end with a file
            directoryPath = path;
        }

        // Normalize the directory path
        var normalized = Services.Tasks.Scanner.Parser.Parser.NormalizePath(directoryPath);
        if (string.IsNullOrEmpty(normalized)) return null;

        normalized = normalized.TrimEnd('/');

        return await _context.Series
            .Where(s => !string.IsNullOrEmpty(s.LowestFolderPath) && EF.Functions.Like(normalized, s.LowestFolderPath + "%"))
            .Includes(includes)
            .SingleOrDefaultAsync();
    }

    public async Task<IEnumerable<Series>> GetAllSeriesByNameAsync(IList<string> normalizedNames,
        int userId, SeriesIncludes includes = SeriesIncludes.None)
    {
        var libraryIds = _context.Library.GetUserLibraries(userId);
        var userRating = await _context.AppUser.GetUserAgeRestriction(userId);

        return await _context.Series
            .Where(s => normalizedNames.Contains(s.NormalizedName) ||
                        normalizedNames.Contains(s.NormalizedLocalizedName))
            .Where(s => libraryIds.Contains(s.LibraryId))
            .RestrictAgainstAgeRestriction(userRating)
            .Includes(includes)
            .ToListAsync();
    }


    /// <summary>
    /// Finds a series by series name or localized name for a given library.
    /// </summary>
    /// <remarks>This pulls everything with the Series, so should be used only when needing tracking on all related tables</remarks>
    /// <param name="seriesName"></param>
    /// <param name="localizedName"></param>
    /// <param name="libraryId"></param>
    /// <param name="format"></param>
    /// <param name="withFullIncludes">Defaults to true. This will query against all foreign keys (deep). If false, just the series will come back</param>
    /// <returns></returns>
    public Task<Series?> GetFullSeriesByAnyName(string seriesName, string localizedName, int libraryId,
        MangaFormat format, bool withFullIncludes = true)
    {
        var normalizedSeries = seriesName.ToNormalized();
        var normalizedLocalized = localizedName.ToNormalized();
        var query = _context.Series
            .Where(s => s.LibraryId == libraryId)
            .Where(s => s.Format == format && format != MangaFormat.Unknown)
            .Where(s =>
                s.NormalizedName.Equals(normalizedSeries)
                || s.NormalizedName.Equals(normalizedLocalized)

                || s.NormalizedLocalizedName.Equals(normalizedSeries)
                || (!string.IsNullOrEmpty(normalizedLocalized) && s.NormalizedLocalizedName.Equals(normalizedLocalized))

                || (s.OriginalName != null && s.OriginalName.Equals(seriesName))
            );
        if (!withFullIncludes)
        {
            return query.SingleOrDefaultAsync();
        }

        #nullable disable
        query = query.Include(s => s.Library)

            .Include(s => s.Metadata)
            .ThenInclude(m => m.People)
            .ThenInclude(p => p.Person)

            .Include(s => s.Metadata)
            .ThenInclude(m => m.Genres)

            .Include(s => s.Metadata)
            .ThenInclude(m => m.Tags)

            .Include(s => s.Volumes)
            .ThenInclude(v => v.Chapters)
            .ThenInclude(cm => cm.People)
            .ThenInclude(p => p.Person)

            .Include(s => s.Volumes)
            .ThenInclude(v => v.Chapters)
            .ThenInclude(c => c.Tags)

            .Include(s => s.Volumes)
            .ThenInclude(v => v.Chapters)
            .ThenInclude(c => c.Genres)

            .Include(s => s.Volumes)
            .ThenInclude(v => v.Chapters)
            .ThenInclude(c => c.Files)

            .AsSplitQuery();
        return query.SingleOrDefaultAsync();

    #nullable enable
    }

    public async Task<Series?> GetSeriesByAnyName(string seriesName, string localizedName, IList<MangaFormat> formats,
        int userId, int? aniListId = null, SeriesIncludes includes = SeriesIncludes.None)
    {
        var libraryIds = GetLibraryIdsForUser(userId);
        var normalizedSeries = seriesName.ToNormalized();
        var normalizedLocalized = localizedName.ToNormalized();

        var query = _context.Series
            .Where(s => libraryIds.Contains(s.LibraryId))
            .Where(s => formats.Contains(s.Format));

        if (aniListId.HasValue && aniListId.Value > 0)
        {
            // If AniList ID is provided, override name checks
            query = query.Where(s => s.ExternalSeriesMetadata.AniListId == aniListId.Value);
        }
        else
        {
            // Otherwise, use name checks
            query = query.Where(s =>
                s.NormalizedName.Equals(normalizedSeries)
                || s.NormalizedName.Equals(normalizedLocalized)
                || s.NormalizedLocalizedName.Equals(normalizedSeries)
                || (!string.IsNullOrEmpty(normalizedLocalized) && s.NormalizedLocalizedName.Equals(normalizedLocalized))
                || (s.OriginalName != null && s.OriginalName.Equals(seriesName))
            );
        }

        return await query
            .Includes(includes)
            .FirstOrDefaultAsync();
    }


    public async Task<Series?> GetSeriesByAnyName(IList<string> names, IList<MangaFormat> formats,
        int userId, int? aniListId = null, SeriesIncludes includes = SeriesIncludes.None)
    {
        var libraryIds = GetLibraryIdsForUser(userId);
        names = names.Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
        var normalizedNames = names.Select(s => s.ToNormalized()).ToList();


        var query = _context.Series
            .Where(s => libraryIds.Contains(s.LibraryId))
            .Where(s => formats.Contains(s.Format));

        if (aniListId.HasValue && aniListId.Value > 0)
        {
            // If AniList ID is provided, override name checks
            query = query.Where(s => s.ExternalSeriesMetadata.AniListId == aniListId.Value ||
                                     normalizedNames.Contains(s.NormalizedName)
                                     || normalizedNames.Contains(s.NormalizedLocalizedName)
                                     || names.Contains(s.OriginalName));
        }
        else
        {
            // Otherwise, use name checks
            query = query.Where(s =>
                normalizedNames.Contains(s.NormalizedName)
                || normalizedNames.Contains(s.NormalizedLocalizedName)
                || names.Contains(s.OriginalName));
        }

        return await query
            .Includes(includes)
            .FirstOrDefaultAsync();
    }

    public async Task<IList<Series>> GetAllSeriesByAnyName(string seriesName, string localizedName, int libraryId,
        MangaFormat format)
    {
        var normalizedSeries = seriesName.ToNormalized();
        var normalizedLocalized = localizedName.ToNormalized();
        return await _context.Series
            .Where(s => s.LibraryId == libraryId)
            .Where(s => s.Format == format && format != MangaFormat.Unknown)
            .Where(s =>
                s.NormalizedName.Equals(normalizedSeries)
                || s.NormalizedName.Equals(normalizedLocalized)

                || s.NormalizedLocalizedName.Equals(normalizedSeries)
                || (!string.IsNullOrEmpty(normalizedLocalized) && s.NormalizedLocalizedName.Equals(normalizedLocalized))

                || (s.OriginalName != null && s.OriginalName.Equals(seriesName))
            )
            .AsSplitQuery()
            .ToListAsync();
    }


    /// <summary>
    /// Removes series that are not in the seenSeries list. Does not commit.
    /// </summary>
    /// <param name="seenSeries"></param>
    /// <param name="libraryId"></param>
    public async Task<IList<Series>> RemoveSeriesNotInList(IList<ParsedSeries> seenSeries, int libraryId)
    {
        if (!seenSeries.Any()) return Array.Empty<Series>();

        // Get all series from DB in one go, based on libraryId
        var dbSeries = await _context.Series
            .Where(s => s.LibraryId == libraryId)
            .ToListAsync();

        // Get a set of matching series ids for the given parsedSeries
        var ids = new HashSet<int>();

        foreach (var parsedSeries in seenSeries)
        {
            var matchingSeries = dbSeries
                .Where(s => s.Format == parsedSeries.Format && s.NormalizedName == parsedSeries.NormalizedName)
                .OrderBy(s => s.Id) // Sort to handle potential duplicates
                .ToList();

            // Prefer the first match or handle duplicates by choosing the last one
            if (matchingSeries.Count != 0)
            {
                ids.Add(matchingSeries.Last().Id);
            }
        }

        // Filter out series that are not in the seenSeries
        var seriesToRemove = dbSeries
            .Where(s => !ids.Contains(s.Id))
            .ToList();

        // Remove series in bulk
        _context.Series.RemoveRange(seriesToRemove);

        return seriesToRemove;
    }

    public async Task<PagedList<SeriesDto>> GetHighlyRated(int userId, int libraryId, UserParams userParams)
    {
        var libraryIds = GetLibraryIdsForUser(userId, libraryId, QueryContext.Recommended)
            .Where(id => libraryId == 0 || id == libraryId);
        var usersSeriesIds = GetSeriesIdsForLibraryIds(libraryIds);
        var distinctSeriesIdsWithHighRating = _context.AppUserRating
            .Where(s => usersSeriesIds.Contains(s.SeriesId) && s.Rating > 4)
            .Select(p => p.SeriesId)
            .Distinct();
        var userRating = await _context.AppUser.GetUserAgeRestriction(userId);

        var query = _context.Series
            .Where(s => distinctSeriesIdsWithHighRating.Contains(s.Id))
            .RestrictAgainstAgeRestriction(userRating)
            .AsSplitQuery()
            .OrderByDescending(s => _context.AppUserRating.Where(r => r.SeriesId == s.Id).Select(r => r.Rating).Average())
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider);

        return await PagedList<SeriesDto>.CreateAsync(query, userParams.PageNumber, userParams.PageSize);
    }


    public async Task<PagedList<SeriesDto>> GetQuickReads(int userId, int libraryId, UserParams userParams)
    {
        var libraryIds = GetLibraryIdsForUser(userId, libraryId, QueryContext.Recommended)
            .Where(id => libraryId == 0 || id == libraryId);
        var usersSeriesIds = GetSeriesIdsForLibraryIds(libraryIds);
        var distinctSeriesIdsWithProgress = _context.AppUserProgresses
            .Where(s => usersSeriesIds.Contains(s.SeriesId))
            .Select(p => p.SeriesId)
            .Distinct();
        var userRating = await _context.AppUser.GetUserAgeRestriction(userId);


        var query = _context.Series
            .Where(s => (
                (s.Pages / ReaderService.AvgPagesPerMinute / 60 < 10 && s.Format != MangaFormat.Epub)
                || (s.WordCount * ReaderService.AvgWordsPerHour < 10 && s.Format == MangaFormat.Epub))
                    && !distinctSeriesIdsWithProgress.Contains(s.Id) &&
                         usersSeriesIds.Contains(s.Id))
            .Where(s => s.Metadata.PublicationStatus != PublicationStatus.OnGoing)
            .RestrictAgainstAgeRestriction(userRating)
            .AsSplitQuery()
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider);


        return await PagedList<SeriesDto>.CreateAsync(query, userParams.PageNumber, userParams.PageSize);
    }

    public async Task<PagedList<SeriesDto>> GetQuickCatchupReads(int userId, int libraryId, UserParams userParams)
    {
        var libraryIds = GetLibraryIdsForUser(userId, libraryId, QueryContext.Recommended)
            .Where(id => libraryId == 0 || id == libraryId);
        var usersSeriesIds = GetSeriesIdsForLibraryIds(libraryIds);
        var distinctSeriesIdsWithProgress = _context.AppUserProgresses
            .Where(s => usersSeriesIds.Contains(s.SeriesId))
            .Select(p => p.SeriesId)
            .Distinct();

        var userRating = await _context.AppUser.GetUserAgeRestriction(userId);


        var query = _context.Series
            .Where(s => (
                            (s.Pages / ReaderService.AvgPagesPerMinute / 60 < 10 && s.Format != MangaFormat.Epub)
                             || (s.WordCount * ReaderService.AvgWordsPerHour < 10 && s.Format == MangaFormat.Epub))
                        && !distinctSeriesIdsWithProgress.Contains(s.Id) &&
                        usersSeriesIds.Contains(s.Id))
            .Where(s => s.Metadata.PublicationStatus == PublicationStatus.OnGoing)
            .RestrictAgainstAgeRestriction(userRating)
            .AsSplitQuery()
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider);


        return await PagedList<SeriesDto>.CreateAsync(query, userParams.PageNumber, userParams.PageSize);
    }

    public async Task<RelatedSeriesDto> GetRelatedSeries(int userId, int seriesId)
    {
        var libraryIds = _context.Library.GetUserLibraries(userId);
        var usersSeriesIds = GetSeriesIdsForLibraryIds(libraryIds);
        var userRating = await _context.AppUser.GetUserAgeRestriction(userId);

        return new RelatedSeriesDto()
        {
            SourceSeriesId = seriesId,
            Adaptations = await GetRelatedSeriesQuery(seriesId, usersSeriesIds, RelationKind.Adaptation, userRating),
            Characters = await GetRelatedSeriesQuery(seriesId, usersSeriesIds, RelationKind.Character, userRating),
            Prequels = await GetRelatedSeriesQuery(seriesId, usersSeriesIds, RelationKind.Prequel, userRating),
            Sequels = await GetRelatedSeriesQuery(seriesId, usersSeriesIds, RelationKind.Sequel, userRating),
            Contains = await GetRelatedSeriesQuery(seriesId, usersSeriesIds, RelationKind.Contains, userRating),
            SideStories = await GetRelatedSeriesQuery(seriesId, usersSeriesIds, RelationKind.SideStory, userRating),
            SpinOffs = await GetRelatedSeriesQuery(seriesId, usersSeriesIds, RelationKind.SpinOff, userRating),
            Others = await GetRelatedSeriesQuery(seriesId, usersSeriesIds, RelationKind.Other, userRating),
            AlternativeSettings = await GetRelatedSeriesQuery(seriesId, usersSeriesIds, RelationKind.AlternativeSetting, userRating),
            AlternativeVersions = await GetRelatedSeriesQuery(seriesId, usersSeriesIds, RelationKind.AlternativeVersion, userRating),
            Doujinshis = await GetRelatedSeriesQuery(seriesId, usersSeriesIds, RelationKind.Doujinshi, userRating),
            Annuals = await GetRelatedSeriesQuery(seriesId, usersSeriesIds, RelationKind.Annual, userRating),
            Parent = await _context.SeriesRelation
                .Where(r => r.TargetSeriesId == seriesId
                                              && usersSeriesIds.Contains(r.TargetSeriesId)
                                              && r.RelationKind != RelationKind.Prequel
                                              && r.RelationKind != RelationKind.Sequel
                                              && r.RelationKind != RelationKind.Edition)
                        .Select(sr => sr.Series)
                .RestrictAgainstAgeRestriction(userRating)
                .AsSplitQuery()
                .AsNoTracking()
                .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
                .ToListAsync(),
            Editions = await GetRelatedSeriesQuery(seriesId, usersSeriesIds, RelationKind.Edition, userRating)
        };
    }

    private IQueryable<int> GetSeriesIdsForLibraryIds(IQueryable<int> libraryIds)
    {
        return _context.Series
            .Where(s => libraryIds.Contains(s.LibraryId))
            .Select(s => s.Id);
    }

    private async Task<IEnumerable<SeriesDto>> GetRelatedSeriesQuery(int seriesId, IEnumerable<int> usersSeriesIds, RelationKind kind, AgeRestriction userRating)
    {
        return await _context.Series.SelectMany(s =>
            s.Relations.Where(sr => sr.RelationKind == kind && sr.SeriesId == seriesId && usersSeriesIds.Contains(sr.TargetSeriesId))
                .Select(sr => sr.TargetSeries))
            .RestrictAgainstAgeRestriction(userRating)
            .AsSplitQuery()
            .AsNoTracking()
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    private async Task<IEnumerable<RecentlyAddedSeries>> GetRecentlyAddedChaptersQuery(int userId)
    {
        var libraryIds = await _context.AppUser
            .Where(u => u.Id == userId)
            .SelectMany(u => u.Libraries)
            .Where(l => l.IncludeInDashboard)
            .Select(l => l.Id)
            .ToListAsync();

        var withinLastWeek = DateTime.Now - TimeSpan.FromDays(12);
        return _context.Chapter
            .Where(c => c.Created >= withinLastWeek).AsNoTracking()
            .Include(c => c.Volume)
            .ThenInclude(v => v.Series)
            .ThenInclude(s => s.Library)
            .OrderByDescending(c => c.Created)
            .Select(c => new RecentlyAddedSeries()
            {
                LibraryId = c.Volume.Series.LibraryId,
                LibraryType = c.Volume.Series.Library.Type,
                Created = c.Created,
                SeriesId = c.Volume.Series.Id,
                SeriesName = c.Volume.Series.Name,
                VolumeId = c.VolumeId,
                ChapterId = c.Id,
                Format = c.Volume.Series.Format,
                ChapterNumber = c.MinNumber + string.Empty, // TODO: Refactor this
                ChapterRange = c.Range, // TODO: Refactor this
                IsSpecial = c.IsSpecial,
                VolumeNumber = c.Volume.MinNumber,
                ChapterTitle = c.Title,
                AgeRating = c.Volume.Series.Metadata.AgeRating
            })
            .AsSplitQuery()
            .Where(c => c.Created >= withinLastWeek && libraryIds.Contains(c.LibraryId))
            .AsEnumerable();
    }

    [Obsolete("Use GetWantToReadForUserV2Async")]
    public async Task<PagedList<SeriesDto>> GetWantToReadForUserAsync(int userId, UserParams userParams, FilterDto filter)
    {
        var libraryIds = await _context.Library.GetUserLibraries(userId).ToListAsync();
        var query = _context.AppUser
            .Where(user => user.Id == userId)
            .SelectMany(u => u.WantToRead)
            .Where(s => libraryIds.Contains(s.Series.LibraryId))
            .Select(w => w.Series)
            .AsSplitQuery()
            .AsNoTracking();

        var filteredQuery = await CreateFilteredSearchQueryable(userId, 0, filter, query);

        return await PagedList<SeriesDto>.CreateAsync(filteredQuery.ProjectTo<SeriesDto>(_mapper.ConfigurationProvider), userParams.PageNumber, userParams.PageSize);
    }

    public async Task<PagedList<SeriesDto>> GetWantToReadForUserV2Async(int userId, UserParams userParams, FilterV2Dto filter)
    {
        var libraryIds = await _context.Library.GetUserLibraries(userId).ToListAsync();
        var seriesIds = await _context.AppUser
            .Where(user => user.Id == userId)
            .SelectMany(u => u.WantToRead)
            .Where(s => libraryIds.Contains(s.Series.LibraryId))
            .Select(w => w.Series.Id)
            .Distinct()
            .ToListAsync();

        var query = await CreateFilteredSearchQueryableV2(userId, filter, QueryContext.None);

        // Apply the Want to Read filtering
        query = query.Where(s => seriesIds.Contains(s.Id));

        var retSeries = query
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
            .AsSplitQuery()
            .AsNoTracking();

        return await PagedList<SeriesDto>.CreateAsync(retSeries, userParams.PageNumber, userParams.PageSize);
    }

    public async Task<IList<Series>> GetWantToReadForUserAsync(int userId)
    {
        var libraryIds = await _context.Library.GetUserLibraries(userId).ToListAsync();
        return await _context.AppUser
            .Where(user => user.Id == userId)
            .SelectMany(u => u.WantToRead)
            .Where(s => libraryIds.Contains(s.Series.LibraryId))
            .Select(w => w.Series)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Uses multiple names to find a match against a series. If not, returns null.
    /// </summary>
    /// <remarks>This does not restrict to the user at all. That is handled at the API level.</remarks>
    public async Task<SeriesDto?> GetSeriesDtoByNamesAndMetadataIds(IEnumerable<string> names, LibraryType libraryType, string aniListUrl, string malUrl)
    {
        var libraryIds = await _context.Library
            .Where(lib => lib.Type == libraryType)
            .Select(l => l.Id)
            .ToListAsync();

        var normalizedNames = names.Select(n => n.ToNormalized()).ToList();
        SeriesDto? result = null;
        if (!string.IsNullOrEmpty(aniListUrl) || !string.IsNullOrEmpty(malUrl))
        {
            // TODO: I can likely work AniList and MalIds from ExternalSeriesMetadata in here
            result =  await _context.Series
                .Where(s => !string.IsNullOrEmpty(s.Metadata.WebLinks))
                .Where(s => libraryIds.Contains(s.Library.Id))
                .WhereIf(!string.IsNullOrEmpty(aniListUrl), s => s.Metadata.WebLinks.Contains(aniListUrl))
                .WhereIf(!string.IsNullOrEmpty(malUrl), s => s.Metadata.WebLinks.Contains(malUrl))
                .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
                .AsSplitQuery()
                .FirstOrDefaultAsync();
        }

        if (result != null) return result;

        return await _context.Series
            .Where(s => normalizedNames.Contains(s.NormalizedName) ||
                        normalizedNames.Contains(s.NormalizedLocalizedName))
            .Where(s => libraryIds.Contains(s.Library.Id))
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
            .AsSplitQuery()
            .FirstOrDefaultAsync(); // Some users may have improperly configured libraries
    }

    public async Task<Series?> MatchSeries(ExternalSeriesDetailDto externalSeries)
    {
        var libraryIds = await _context.Library
            .Where(lib => externalSeries.PlusMediaFormat.ConvertToLibraryTypes().Contains(lib.Type))
            .Select(l => l.Id)
            .ToListAsync();

        var normalizedNames = (externalSeries.Synonyms ?? Enumerable.Empty<string>())
            .Prepend(externalSeries.Name)
            .Select(n => n.ToNormalized())
            .ToList();

        var aniListWebLink =
            ScrobblingService.CreateUrl(ScrobblingService.AniListWeblinkWebsite, externalSeries.AniListId);
        var malWebLink =
            ScrobblingService.CreateUrl(ScrobblingService.MalWeblinkWebsite, externalSeries.MALId);

        Series? result = null;
        if (!string.IsNullOrEmpty(aniListWebLink) || !string.IsNullOrEmpty(malWebLink))
        {
            result = await _context.Series
                .Where(s => !string.IsNullOrEmpty(s.Metadata.WebLinks))
                .Where(s => libraryIds.Contains(s.Library.Id))
                .WhereIf(!string.IsNullOrEmpty(aniListWebLink), s => s.Metadata.WebLinks.Contains(aniListWebLink))
                .WhereIf(!string.IsNullOrEmpty(malWebLink), s => s.Metadata.WebLinks.Contains(malWebLink))
                .Include(s => s.Metadata)
                .AsSplitQuery()
                .FirstOrDefaultAsync();
        }

        if (result != null) return result;

        return await _context.Series
            .Where(s => normalizedNames.Contains(s.NormalizedName) ||
                        normalizedNames.Contains(s.NormalizedLocalizedName))
            .Where(s => libraryIds.Contains(s.Library.Id))
            .AsSplitQuery()
            .Include(s => s.Metadata)
            .FirstOrDefaultAsync(); // Some users may have improperly configured libraries
    }

    /// <summary>
    /// Returns the Average rating for all users within Kavita instance
    /// </summary>
    /// <param name="seriesId"></param>
    public async Task<int> GetAverageUserRating(int seriesId, int userId)
    {
        // If there is 0 or 1 rating and that rating is you, return 0 back
        var countOfRatingsThatAreUser = await _context.AppUserRating
            .Where(r => r.SeriesId == seriesId && r.HasBeenRated)
            .CountAsync(u => u.AppUserId == userId);
        if (countOfRatingsThatAreUser == 1)
        {
            return 0;
        }
        var avg = (await _context.AppUserRating
            .Where(r => r.SeriesId == seriesId && r.HasBeenRated)
            .AverageAsync(r => (int?) r.Rating));
        return avg.HasValue ? (int) (avg.Value * 20) : 0;
    }

    public async Task RemoveFromOnDeck(int seriesId, int userId)
    {
        var existingEntry = await _context.AppUserOnDeckRemoval
            .Where(u => u.Id == userId && u.SeriesId == seriesId)
            .AnyAsync();
        if (existingEntry) return;
        _context.AppUserOnDeckRemoval.Add(new AppUserOnDeckRemoval()
        {
            SeriesId = seriesId,
            AppUserId = userId
        });
        await _context.SaveChangesAsync();
    }

    public async Task ClearOnDeckRemoval(int seriesId, int userId)
    {
        var existingEntry = await _context.AppUserOnDeckRemoval
            .Where(u => u.AppUserId == userId && u.SeriesId == seriesId)
            .FirstOrDefaultAsync();
        if (existingEntry == null) return;
        _context.AppUserOnDeckRemoval.Remove(existingEntry);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> IsSeriesInWantToRead(int userId, int seriesId)
    {
        var libraryIds = await _context.Library.GetUserLibraries(userId).ToListAsync();
        return await _context.AppUser
            .Where(user => user.Id == userId)
            .SelectMany(u => u.WantToRead.Where(s => s.SeriesId == seriesId && libraryIds.Contains(s.Series.LibraryId)))
            .AsSplitQuery()
            .AsNoTracking()
            .AnyAsync();
    }

    public async Task<IDictionary<string, IList<SeriesModified>>> GetFolderPathMap(int libraryId)
    {
        var info = await _context.Series
            .Where(s => s.LibraryId == libraryId)
            .AsNoTracking()
            .Where(s => s.FolderPath != null)
            .Select(s => new SeriesModified()
            {
                LastScanned = s.LastFolderScanned,
                SeriesName = s.Name,
                FolderPath = s.FolderPath,
                LowestFolderPath = s.LowestFolderPath,
                Format = s.Format,
                LibraryRoots = s.Library.Folders.Select(f => f.Path)
            })
            .ToListAsync();

        var map = new Dictionary<string, IList<SeriesModified>>();
        foreach (var series in info)
        {
            if (string.IsNullOrEmpty(series.FolderPath)) continue;
            if (!map.TryGetValue(series.FolderPath, out var value))
            {
                map.Add(series.FolderPath, new List<SeriesModified>()
                {
                    series
                });
            }
            else
            {
                value.Add(series);
            }


            if (string.IsNullOrEmpty(series.LowestFolderPath) || series.FolderPath.Equals(series.LowestFolderPath)) continue;
            if (!map.TryGetValue(series.LowestFolderPath, out var value2))
            {
                map.Add(series.LowestFolderPath, new List<SeriesModified>()
                {
                    series
                });
            }
            else
            {
                value2.Add(series);
            }
        }

        return map;
    }

    /// <summary>
    /// Returns the highest Age Rating for a list of Series. Defaults to <see cref="AgeRating.Unknown"/>
    /// </summary>
    /// <param name="seriesIds"></param>
    /// <returns></returns>
    public async Task<AgeRating> GetMaxAgeRatingFromSeriesAsync(IEnumerable<int> seriesIds)
    {
        var ret = await _context.Series
            .Where(s => seriesIds.Contains(s.Id))
            .Include(s => s.Metadata)
            .Select(s => s.Metadata.AgeRating)
            .OrderBy(s => s)
            .LastOrDefaultAsync();
        if (ret == null) return AgeRating.Unknown;
        return ret;
    }

    /// <summary>
    /// Returns all library ids for a user
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="libraryId">0 for no library filter</param>
    /// <param name="queryContext">Defaults to None - The context behind this query, so appropriate restrictions can be placed</param>
    /// <returns></returns>
    private IQueryable<int> GetLibraryIdsForUser(int userId, int libraryId = 0, QueryContext queryContext = QueryContext.None)
    {
        var user = _context.AppUser
            .AsSplitQuery()
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .AsSingleQuery();

        if (libraryId == 0)
        {
            return user.SelectMany(l => l.Libraries)
                .IsRestricted(queryContext)
                .Select(lib => lib.Id);
        }

        return user.SelectMany(l => l.Libraries)
            .Where(lib => lib.Id == libraryId)
            .IsRestricted(queryContext)
            .Select(lib => lib.Id);
    }

}
