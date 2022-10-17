using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using API.Data.Misc;
using API.Data.Scanner;
using API.DTOs;
using API.DTOs.CollectionTags;
using API.DTOs.Filtering;
using API.DTOs.Metadata;
using API.DTOs.ReadingLists;
using API.DTOs.Search;
using API.DTOs.SeriesDetail;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Extensions;
using API.Helpers;
using API.Services;
using API.Services.Tasks;
using API.Services.Tasks.Scanner;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;


namespace API.Data.Repositories;

[Flags]
public enum SeriesIncludes
{
    None = 1,
    Volumes = 2,
    Metadata = 4,
    Related = 8,
    Library = 16,
}

public interface ISeriesRepository
{
    void Add(Series series);
    void Attach(Series series);
    void Update(Series series);
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
    /// <returns></returns>
    Task<SearchResultGroupDto> SearchSeries(int userId, bool isAdmin, int[] libraryIds, string searchQuery);
    Task<IEnumerable<Series>> GetSeriesForLibraryIdAsync(int libraryId);
    Task<SeriesDto> GetSeriesDtoByIdAsync(int seriesId, int userId);
    Task<bool> DeleteSeriesAsync(int seriesId);
    Task<Series> GetSeriesByIdAsync(int seriesId, SeriesIncludes includes = SeriesIncludes.Volumes | SeriesIncludes.Metadata);
    Task<IList<Series>> GetSeriesByIdsAsync(IList<int> seriesIds);
    Task<int[]> GetChapterIdsForSeriesAsync(IList<int> seriesIds);
    Task<IDictionary<int, IList<int>>> GetChapterIdWithSeriesIdForSeriesAsync(int[] seriesIds);
    /// <summary>
    /// Used to add Progress/Rating information to series list.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="series"></param>
    /// <returns></returns>
    Task AddSeriesModifiers(int userId, List<SeriesDto> series);
    Task<string> GetSeriesCoverImageAsync(int seriesId);
    Task<PagedList<SeriesDto>> GetOnDeck(int userId, int libraryId, UserParams userParams, FilterDto filter);
    Task<PagedList<SeriesDto>> GetRecentlyAdded(int libraryId, int userId, UserParams userParams, FilterDto filter);
    Task<SeriesMetadataDto> GetSeriesMetadata(int seriesId);
    Task<PagedList<SeriesDto>> GetSeriesDtoForCollectionAsync(int collectionId, int userId, UserParams userParams);
    Task<IList<MangaFile>> GetFilesForSeries(int seriesId);
    Task<IEnumerable<SeriesDto>> GetSeriesDtoForIdsAsync(IEnumerable<int> seriesIds, int userId);
    Task<IList<string>> GetAllCoverImagesAsync();
    Task<IEnumerable<string>> GetLockedCoverImagesAsync();
    Task<PagedList<Series>> GetFullSeriesForLibraryIdAsync(int libraryId, UserParams userParams);
    Task<Series> GetFullSeriesForSeriesIdAsync(int seriesId);
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
    Task<SeriesDto> GetSeriesForMangaFile(int mangaFileId, int userId);
    Task<SeriesDto> GetSeriesForChapter(int chapterId, int userId);
    Task<PagedList<SeriesDto>> GetWantToReadForUserAsync(int userId, UserParams userParams, FilterDto filter);
    Task<Series> GetSeriesByFolderPath(string folder, SeriesIncludes includes = SeriesIncludes.None);
    Task<Series> GetFullSeriesByAnyName(string seriesName, string localizedName, int libraryId, MangaFormat format, bool withFullIncludes = true);
    Task<IList<Series>> RemoveSeriesNotInList(IList<ParsedSeries> seenSeries, int libraryId);
    Task<IDictionary<string, IList<SeriesModified>>> GetFolderPathMap(int libraryId);
    Task<AgeRating> GetMaxAgeRatingFromSeriesAsync(IEnumerable<int> seriesIds);
}

public class SeriesRepository : ISeriesRepository
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;
    public SeriesRepository(DataContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public void Add(Series series)
    {
        _context.Series.Add(series);
    }

    public void Attach(Series series)
    {
        _context.Series.Attach(series);
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


    public async Task<IEnumerable<Series>> GetSeriesForLibraryIdAsync(int libraryId)
    {
        return await _context.Series
            .Where(s => s.LibraryId == libraryId)
            .OrderBy(s => s.SortName)
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
        var query = _context.Series
            .Where(s => s.LibraryId == libraryId)

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

            .Include(s => s.Volumes)
            .ThenInclude(v => v.Chapters)
            .ThenInclude(c => c.Files)
            .AsSplitQuery()
            .OrderBy(s => s.SortName);

        return await PagedList<Series>.CreateAsync(query, userParams.PageNumber, userParams.PageSize);
    }

    /// <summary>
    /// This is a heavy call. Returns all entities down to Files and Library and Series Metadata.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    public async Task<Series> GetFullSeriesForSeriesIdAsync(int seriesId)
    {
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
    }

    /// <summary>
    /// Gets all series
    /// </summary>
    /// <param name="libraryId">Restricts to just one library</param>
    /// <param name="userId"></param>
    /// <param name="userParams"></param>
    /// <param name="filter"></param>
    /// <returns></returns>
    public async Task<PagedList<SeriesDto>> GetSeriesDtoForLibraryIdAsync(int libraryId, int userId, UserParams userParams, FilterDto filter)
    {
        var query = await CreateFilteredSearchQueryable(userId, libraryId, filter);

        var retSeries = query
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
            .AsSplitQuery()
            .AsNoTracking();

        return await PagedList<SeriesDto>.CreateAsync(retSeries, userParams.PageNumber, userParams.PageSize);
    }

    private async Task<List<int>> GetUserLibraries(int libraryId, int userId)
    {
        if (libraryId == 0)
        {
            return await _context.Library
                .Include(l => l.AppUsers)
                .Where(library => library.AppUsers.Any(user => user.Id == userId))
                .AsNoTracking()
                .AsSplitQuery()
                .Select(library => library.Id)
                .ToListAsync();
        }

        return new List<int>()
        {
            libraryId
        };
    }

    public async Task<SearchResultGroupDto> SearchSeries(int userId, bool isAdmin, int[] libraryIds, string searchQuery)
    {
        const int maxRecords = 15;
        var result = new SearchResultGroupDto();
        var searchQueryNormalized = Services.Tasks.Scanner.Parser.Parser.Normalize(searchQuery);
        var userRating = await GetUserAgeRestriction(userId);

        var seriesIds = _context.Series
            .Where(s => libraryIds.Contains(s.LibraryId))
            .RestrictAgainstAgeRestriction(userRating)
            .Select(s => s.Id)
            .ToList();

        result.Libraries = await _context.Library
            .Where(l => libraryIds.Contains(l.Id))
            .Where(l => EF.Functions.Like(l.Name, $"%{searchQuery}%"))
            .OrderBy(l => l.Name)
            .AsSplitQuery()
            .Take(maxRecords)
            .ProjectTo<LibraryDto>(_mapper.ConfigurationProvider)
            .ToListAsync();

        var justYear = Regex.Match(searchQuery, @"\d{4}").Value;
        var hasYearInQuery = !string.IsNullOrEmpty(justYear);
        var yearComparison = hasYearInQuery ? int.Parse(justYear) : 0;

        result.Series = _context.Series
            .Where(s => libraryIds.Contains(s.LibraryId))
            .Where(s => EF.Functions.Like(s.Name, $"%{searchQuery}%")
                        || EF.Functions.Like(s.OriginalName, $"%{searchQuery}%")
                        || EF.Functions.Like(s.LocalizedName, $"%{searchQuery}%")
                        || EF.Functions.Like(s.NormalizedName, $"%{searchQueryNormalized}%")
                        || (hasYearInQuery && s.Metadata.ReleaseYear == yearComparison))
            .RestrictAgainstAgeRestriction(userRating)
            .Include(s => s.Library)
            .OrderBy(s => s.SortName)
            .AsNoTracking()
            .AsSplitQuery()
            .Take(maxRecords)
            .ProjectTo<SearchResultDto>(_mapper.ConfigurationProvider)
            .AsEnumerable();

        result.ReadingLists = await _context.ReadingList
            .Where(rl => rl.AppUserId == userId || rl.Promoted)
            .Where(rl => EF.Functions.Like(rl.Title, $"%{searchQuery}%"))
            .RestrictAgainstAgeRestriction(userRating)
            .AsSplitQuery()
            .Take(maxRecords)
            .ProjectTo<ReadingListDto>(_mapper.ConfigurationProvider)
            .ToListAsync();

        result.Collections =  await _context.CollectionTag
            .Where(c => EF.Functions.Like(c.Title, $"%{searchQuery}%")
                        || EF.Functions.Like(c.NormalizedTitle, $"%{searchQueryNormalized}%"))
            .Where(c => c.Promoted || isAdmin)
            .RestrictAgainstAgeRestriction(userRating)
            .OrderBy(s => s.Title)
            .AsNoTracking()
            .AsSplitQuery()
            .Take(maxRecords)
            .OrderBy(c => c.NormalizedTitle)
            .ProjectTo<CollectionTagDto>(_mapper.ConfigurationProvider)
            .ToListAsync();

        result.Persons = await _context.SeriesMetadata
            .Where(sm => seriesIds.Contains(sm.SeriesId))
            .SelectMany(sm => sm.People.Where(t => EF.Functions.Like(t.Name, $"%{searchQuery}%")))
            .AsSplitQuery()
            .Take(maxRecords)
            .Distinct()
            .ProjectTo<PersonDto>(_mapper.ConfigurationProvider)
            .ToListAsync();

        result.Genres = await _context.SeriesMetadata
            .Where(sm => seriesIds.Contains(sm.SeriesId))
            .SelectMany(sm => sm.Genres.Where(t => EF.Functions.Like(t.Title, $"%{searchQuery}%")))
            .AsSplitQuery()
            .OrderBy(t => t.Title)
            .Distinct()
            .Take(maxRecords)
            .ProjectTo<GenreTagDto>(_mapper.ConfigurationProvider)
            .ToListAsync();

        result.Tags = await _context.SeriesMetadata
            .Where(sm => seriesIds.Contains(sm.SeriesId))
            .SelectMany(sm => sm.Tags.Where(t => EF.Functions.Like(t.Title, $"%{searchQuery}%")))
            .AsSplitQuery()
            .OrderBy(t => t.Title)
            .Distinct()
            .Take(maxRecords)
            .ProjectTo<TagDto>(_mapper.ConfigurationProvider)
            .ToListAsync();

        var fileIds = _context.Series
            .Where(s => seriesIds.Contains(s.Id))
            .AsSplitQuery()
            .SelectMany(s => s.Volumes)
            .SelectMany(v => v.Chapters)
            .SelectMany(c => c.Files.Select(f => f.Id));

        result.Files = await _context.MangaFile
            .Where(m => EF.Functions.Like(m.FilePath, $"%{searchQuery}%") && fileIds.Contains(m.Id))
            .AsSplitQuery()
            .Take(maxRecords)
            .ProjectTo<MangaFileDto>(_mapper.ConfigurationProvider)
            .ToListAsync();


        result.Chapters = await _context.Chapter
            .Include(c => c.Files)
            .Where(c => EF.Functions.Like(c.TitleName, $"%{searchQuery}%"))
            .Where(c => c.Files.All(f => fileIds.Contains(f.Id)))
            .AsSplitQuery()
            .Take(maxRecords)
            .ProjectTo<ChapterDto>(_mapper.ConfigurationProvider)
            .ToListAsync();

        return result;
    }

    public async Task<SeriesDto> GetSeriesDtoByIdAsync(int seriesId, int userId)
    {
        var series = await _context.Series.Where(x => x.Id == seriesId)
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
            .SingleAsync();

        var seriesList = new List<SeriesDto>() {series};
        await AddSeriesModifiers(userId, seriesList);

        return seriesList[0];
    }

    public async Task<bool> DeleteSeriesAsync(int seriesId)
    {
        var series = await _context.Series.Where(s => s.Id == seriesId).SingleOrDefaultAsync();
        if (series != null) _context.Series.Remove(series);

        return await _context.SaveChangesAsync() > 0;
    }


    /// <summary>
    /// Returns Volumes, Metadata (Incl Genres and People), and Collection Tags
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="includes"></param>
    /// <returns></returns>
    public async Task<Series> GetSeriesByIdAsync(int seriesId, SeriesIncludes includes = SeriesIncludes.Volumes | SeriesIncludes.Metadata)
    {
        var query = _context.Series
            .Where(s => s.Id == seriesId)
            .AsSplitQuery();

         if (includes.HasFlag(SeriesIncludes.Volumes))
         {
             query = query.Include(s => s.Volumes);
         }

         if (includes.HasFlag(SeriesIncludes.Related))
         {
             query = query.Include(s => s.Relations)
                 .ThenInclude(r => r.TargetSeries)
                 .Include(s => s.RelationOf);
         }

         if (includes.HasFlag(SeriesIncludes.Metadata))
         {
             query = query.Include(s => s.Metadata)
                 .ThenInclude(m => m.CollectionTags)
                 .Include(s => s.Metadata)
                 .ThenInclude(m => m.Genres)
                 .Include(s => s.Metadata)
                 .ThenInclude(m => m.People)
                 .Include(s => s.Metadata)
                 .ThenInclude(m => m.Tags);
         }

         return await query.SingleOrDefaultAsync();
    }

    /// <summary>
    /// Returns Volumes, Metadata, and Collection Tags
    /// </summary>
    /// <param name="seriesIds"></param>
    /// <returns></returns>
    public async Task<IList<Series>> GetSeriesByIdsAsync(IList<int> seriesIds)
    {
        return await _context.Series
            .Include(s => s.Volumes)
            .Include(s => s.Metadata)
            .ThenInclude(m => m.CollectionTags)
            .Include(s => s.Relations)
            .Where(s => seriesIds.Contains(s.Id))
            .AsSplitQuery()
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

    public async Task AddSeriesModifiers(int userId, List<SeriesDto> series)
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
                s.UserReview = rating.Review;
            }

            if (userProgress.Count > 0)
            {
                s.LatestReadDate = userProgress.Max(p => p.LastModified);
            }
        }
    }

    public async Task<string> GetSeriesCoverImageAsync(int seriesId)
    {
        return await _context.Series
            .Where(s => s.Id == seriesId)
            .Select(s => s.CoverImage)
            .AsNoTracking()
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
    public async Task<PagedList<SeriesDto>> GetRecentlyAdded(int libraryId, int userId, UserParams userParams, FilterDto filter)
    {
        var query = await CreateFilteredSearchQueryable(userId, libraryId, filter);

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
    public async Task<PagedList<SeriesDto>> GetOnDeck(int userId, int libraryId, UserParams userParams, FilterDto filter)
    {
        var cutoffProgressPoint = DateTime.Now - TimeSpan.FromDays(30);
        var cutoffLastAddedPoint = DateTime.Now - TimeSpan.FromDays(7);

        var libraryIds = GetLibraryIdsForUser(userId, libraryId);
        var usersSeriesIds = GetSeriesIdsForLibraryIds(libraryIds);


        var query = _context.Series
            .Where(s => usersSeriesIds.Contains(s.Id))
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
            .Where(d => d.LatestReadDate >= cutoffProgressPoint || d.LastChapterAdded >= cutoffLastAddedPoint).OrderByDescending(s => s.LatestReadDate)
            .ThenByDescending(s => s.LastChapterAdded)
            .Select(s => s.Series)
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
            .AsSplitQuery()
            .AsNoTracking();

        return await PagedList<SeriesDto>.CreateAsync(query, userParams.PageNumber, userParams.PageSize);
    }

    private async Task<IQueryable<Series>> CreateFilteredSearchQueryable(int userId, int libraryId, FilterDto filter)
    {
        var userLibraries = await GetUserLibraries(libraryId, userId);
        var userRating = await GetUserAgeRestriction(userId);

        var formats = ExtractFilters(libraryId, userId, filter, ref userLibraries,
            out var allPeopleIds, out var hasPeopleFilter, out var hasGenresFilter,
            out var hasCollectionTagFilter, out var hasRatingFilter, out var hasProgressFilter,
            out var seriesIds, out var hasAgeRating, out var hasTagsFilter, out var hasLanguageFilter,
            out var hasPublicationFilter, out var hasSeriesNameFilter, out var hasReleaseYearMinFilter, out var hasReleaseYearMaxFilter);

        var query = _context.Series
            .Where(s => userLibraries.Contains(s.LibraryId)
                        && formats.Contains(s.Format)
                        && (!hasGenresFilter || s.Metadata.Genres.Any(g => filter.Genres.Contains(g.Id)))
                        && (!hasPeopleFilter || s.Metadata.People.Any(p => allPeopleIds.Contains(p.Id)))
                        && (!hasCollectionTagFilter ||
                            s.Metadata.CollectionTags.Any(t => filter.CollectionTags.Contains(t.Id)))
                        && (!hasRatingFilter || s.Ratings.Any(r => r.Rating >= filter.Rating && r.AppUserId == userId))
                        && (!hasProgressFilter || seriesIds.Contains(s.Id))
                        && (!hasAgeRating || filter.AgeRating.Contains(s.Metadata.AgeRating))
                        && (!hasTagsFilter || s.Metadata.Tags.Any(t => filter.Tags.Contains(t.Id)))
                        && (!hasLanguageFilter || filter.Languages.Contains(s.Metadata.Language))
                        && (!hasReleaseYearMinFilter || s.Metadata.ReleaseYear >= filter.ReleaseYearRange.Min)
                        && (!hasReleaseYearMaxFilter || s.Metadata.ReleaseYear <= filter.ReleaseYearRange.Max)
                        && (!hasPublicationFilter || filter.PublicationStatus.Contains(s.Metadata.PublicationStatus)))
            .Where(s => !hasSeriesNameFilter ||
                        EF.Functions.Like(s.Name, $"%{filter.SeriesNameQuery}%")
                                             || EF.Functions.Like(s.OriginalName, $"%{filter.SeriesNameQuery}%")
                                             || EF.Functions.Like(s.LocalizedName, $"%{filter.SeriesNameQuery}%"));
        if (userRating.AgeRating != AgeRating.NotApplicable)
        {
            query = query.RestrictAgainstAgeRestriction(userRating);
        }

        query = query.AsNoTracking();

        // If no sort options, default to using SortName
        filter.SortOptions ??= new SortOptions()
        {
            IsAscending = true,
            SortField = SortField.SortName
        };

        if (filter.SortOptions.IsAscending)
        {
            query = filter.SortOptions.SortField switch
            {
                SortField.SortName => query.OrderBy(s => s.SortName),
                SortField.CreatedDate => query.OrderBy(s => s.Created),
                SortField.LastModifiedDate => query.OrderBy(s => s.LastModified),
                SortField.LastChapterAdded => query.OrderBy(s => s.LastChapterAdded),
                SortField.TimeToRead => query.OrderBy(s => s.AvgHoursToRead),
                SortField.ReleaseYear => query.OrderBy(s => s.Metadata.ReleaseYear),
                _ => query
            };
        }
        else
        {
            query = filter.SortOptions.SortField switch
            {
                SortField.SortName => query.OrderByDescending(s => s.SortName),
                SortField.CreatedDate => query.OrderByDescending(s => s.Created),
                SortField.LastModifiedDate => query.OrderByDescending(s => s.LastModified),
                SortField.LastChapterAdded => query.OrderByDescending(s => s.LastChapterAdded),
                SortField.TimeToRead => query.OrderByDescending(s => s.AvgHoursToRead),
                SortField.ReleaseYear => query.OrderByDescending(s => s.Metadata.ReleaseYear),
                _ => query
            };
        }

        return query;
    }

    private async Task<IQueryable<Series>> CreateFilteredSearchQueryable(int userId, int libraryId, FilterDto filter, IQueryable<Series> sQuery)
    {
        var userLibraries = await GetUserLibraries(libraryId, userId);
        var formats = ExtractFilters(libraryId, userId, filter, ref userLibraries,
            out var allPeopleIds, out var hasPeopleFilter, out var hasGenresFilter,
            out var hasCollectionTagFilter, out var hasRatingFilter, out var hasProgressFilter,
            out var seriesIds, out var hasAgeRating, out var hasTagsFilter, out var hasLanguageFilter,
            out var hasPublicationFilter, out var hasSeriesNameFilter, out var hasReleaseYearMinFilter, out var hasReleaseYearMaxFilter);

        var query = sQuery
            .Where(s => userLibraries.Contains(s.LibraryId)
                        && formats.Contains(s.Format)
                        && (!hasGenresFilter || s.Metadata.Genres.Any(g => filter.Genres.Contains(g.Id)))
                        && (!hasPeopleFilter || s.Metadata.People.Any(p => allPeopleIds.Contains(p.Id)))
                        && (!hasCollectionTagFilter ||
                            s.Metadata.CollectionTags.Any(t => filter.CollectionTags.Contains(t.Id)))
                        && (!hasRatingFilter || s.Ratings.Any(r => r.Rating >= filter.Rating && r.AppUserId == userId))
                        && (!hasProgressFilter || seriesIds.Contains(s.Id))
                        && (!hasAgeRating || filter.AgeRating.Contains(s.Metadata.AgeRating))
                        && (!hasTagsFilter || s.Metadata.Tags.Any(t => filter.Tags.Contains(t.Id)))
                        && (!hasLanguageFilter || filter.Languages.Contains(s.Metadata.Language))
                        && (!hasReleaseYearMinFilter || s.Metadata.ReleaseYear >= filter.ReleaseYearRange.Min)
                        && (!hasReleaseYearMaxFilter || s.Metadata.ReleaseYear <= filter.ReleaseYearRange.Max)
                        && (!hasPublicationFilter || filter.PublicationStatus.Contains(s.Metadata.PublicationStatus)))
            .Where(s => !hasSeriesNameFilter ||
                        EF.Functions.Like(s.Name, $"%{filter.SeriesNameQuery}%")
                                             || EF.Functions.Like(s.OriginalName, $"%{filter.SeriesNameQuery}%")
                                             || EF.Functions.Like(s.LocalizedName, $"%{filter.SeriesNameQuery}%"))
            .AsNoTracking();

        // If no sort options, default to using SortName
        filter.SortOptions ??= new SortOptions()
        {
            IsAscending = true,
            SortField = SortField.SortName
        };

        if (filter.SortOptions.IsAscending)
        {
            query = filter.SortOptions.SortField switch
            {
                SortField.SortName => query.OrderBy(s => s.SortName),
                SortField.CreatedDate => query.OrderBy(s => s.Created),
                SortField.LastModifiedDate => query.OrderBy(s => s.LastModified),
                SortField.LastChapterAdded => query.OrderBy(s => s.LastChapterAdded),
                SortField.TimeToRead => query.OrderBy(s => s.AvgHoursToRead),
                _ => query
            };
        }
        else
        {
            query = filter.SortOptions.SortField switch
            {
                SortField.SortName => query.OrderByDescending(s => s.SortName),
                SortField.CreatedDate => query.OrderByDescending(s => s.Created),
                SortField.LastModifiedDate => query.OrderByDescending(s => s.LastModified),
                SortField.LastChapterAdded => query.OrderByDescending(s => s.LastChapterAdded),
                SortField.TimeToRead => query.OrderByDescending(s => s.AvgHoursToRead),
                _ => query
            };
        }

        return query;
    }

    public async Task<SeriesMetadataDto> GetSeriesMetadata(int seriesId)
    {
        var metadataDto = await _context.SeriesMetadata
            .Where(metadata => metadata.SeriesId == seriesId)
            .Include(m => m.Genres)
            .Include(m => m.Tags)
            .Include(m => m.People)
            .AsNoTracking()
            .ProjectTo<SeriesMetadataDto>(_mapper.ConfigurationProvider)
            .AsSplitQuery()
            .SingleOrDefaultAsync();

        if (metadataDto != null)
        {
            metadataDto.CollectionTags = await _context.CollectionTag
                .Include(t => t.SeriesMetadatas)
                .Where(t => t.SeriesMetadatas.Select(s => s.SeriesId).Contains(seriesId))
                .ProjectTo<CollectionTagDto>(_mapper.ConfigurationProvider)
                .AsNoTracking()
                .AsSplitQuery()
                .ToListAsync();
        }

        return metadataDto;
    }

    public async Task<PagedList<SeriesDto>> GetSeriesDtoForCollectionAsync(int collectionId, int userId, UserParams userParams)
    {
        var userLibraries = _context.Library
            .Include(l => l.AppUsers)
            .Where(library => library.AppUsers.Any(user => user.Id == userId))
            .AsSplitQuery()
            .AsNoTracking()
            .Select(library => library.Id)
            .ToList();

        var query =  _context.CollectionTag
            .Where(s => s.Id == collectionId)
            .Include(c => c.SeriesMetadatas)
            .ThenInclude(m => m.Series)
            .SelectMany(c => c.SeriesMetadatas.Select(sm => sm.Series).Where(s => userLibraries.Contains(s.LibraryId)))
            .OrderBy(s => s.LibraryId)
            .ThenBy(s => s.SortName)
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
            .AsSplitQuery()
            .AsNoTracking();

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

        return await _context.Series
            .Where(s => seriesIds.Contains(s.Id) && allowedLibraries.Contains(s.LibraryId))
            .OrderBy(s => s.SortName)
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
            .AsNoTracking()
            .AsSplitQuery()
            .ToListAsync();
    }

    public async Task<IList<string>> GetAllCoverImagesAsync()
    {
        return await _context.Series
            .Select(s => s.CoverImage)
            .Where(t => !string.IsNullOrEmpty(t))
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<string>> GetLockedCoverImagesAsync()
    {
        return await _context.Series
            .Where(s => s.CoverImageLocked && !string.IsNullOrEmpty(s.CoverImage))
            .Select(s => s.CoverImage)
            .AsNoTracking()
            .ToListAsync();
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
         var userRating = await GetUserAgeRestriction(userId);

         var items = (await GetRecentlyAddedChaptersQuery(userId));
         if (userRating.AgeRating != AgeRating.NotApplicable)
         {
             items = items.RestrictAgainstAgeRestriction(userRating); //.Where(c => c.AgeRating <= userRating);
         }
         foreach (var item in items)
         {
             if (seriesMap.Keys.Count == pageSize) break;

             if (seriesMap.ContainsKey(item.SeriesName))
             {
                 seriesMap[item.SeriesName].Count += 1;
             }
             else
             {
                 seriesMap[item.SeriesName] = new GroupedSeriesDto()
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

    private async Task<AgeRestriction> GetUserAgeRestriction(int userId)
    {
        return await _context.AppUser
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u =>
            new AgeRestriction(){
                AgeRating = u.AgeRestriction,
                IncludeUnknowns = u.AgeRestrictionIncludeUnknowns
            })
            .SingleAsync();
    }

    public async Task<IEnumerable<SeriesDto>> GetSeriesForRelationKind(int userId, int seriesId, RelationKind kind)
    {
        var libraryIds = GetLibraryIdsForUser(userId);
        var userRating = await GetUserAgeRestriction(userId);

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
        var libraryIds = GetLibraryIdsForUser(userId, libraryId);
        var usersSeriesIds = GetSeriesIdsForLibraryIds(libraryIds);

        var query = _context.Series
            .Where(s => s.Metadata.Genres.Select(g => g.Id).Contains(genreId))
            .Where(s => usersSeriesIds.Contains(s.Id))
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
        var libraryIds = GetLibraryIdsForUser(userId, libraryId);
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

    public async Task<SeriesDto> GetSeriesForMangaFile(int mangaFileId, int userId)
    {
        var libraryIds = GetLibraryIdsForUser(userId);
        var userRating = await GetUserAgeRestriction(userId);

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

    public async Task<SeriesDto> GetSeriesForChapter(int chapterId, int userId)
    {
        var libraryIds = GetLibraryIdsForUser(userId);
        var userRating = await GetUserAgeRestriction(userId);
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
    /// <param name="folder">This will be normalized in the query</param>
    /// <param name="includes">Additional relationships to include with the base query</param>
    /// <returns></returns>
    public async Task<Series> GetSeriesByFolderPath(string folder, SeriesIncludes includes = SeriesIncludes.None)
    {
        var normalized = Services.Tasks.Scanner.Parser.Parser.NormalizePath(folder);
        var query = _context.Series.Where(s => s.FolderPath.Equals(normalized));

        query = AddIncludesToQuery(query, includes);

        return await query.SingleOrDefaultAsync();
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
    public Task<Series> GetFullSeriesByAnyName(string seriesName, string localizedName, int libraryId, MangaFormat format, bool withFullIncludes = true)
    {
        var normalizedSeries = Services.Tasks.Scanner.Parser.Parser.Normalize(seriesName);
        var normalizedLocalized = Services.Tasks.Scanner.Parser.Parser.Normalize(localizedName);
        var query = _context.Series
            .Where(s => s.LibraryId == libraryId)
            .Where(s => s.Format == format && format != MangaFormat.Unknown)
            .Where(s => s.NormalizedName.Equals(normalizedSeries)
                        || (s.NormalizedLocalizedName.Equals(normalizedSeries) && s.NormalizedLocalizedName != string.Empty)
                        || s.OriginalName.Equals(seriesName));

        if (!string.IsNullOrEmpty(normalizedLocalized))
        {
            query = query.Where(s =>
                s.NormalizedName.Equals(normalizedLocalized) || s.NormalizedLocalizedName.Equals(normalizedLocalized));
        }

        if (!withFullIncludes)
        {
            return query.SingleOrDefaultAsync();
        }

        return query.Include(s => s.Metadata)
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
    }


    /// <summary>
    /// Removes series that are not in the seenSeries list. Does not commit.
    /// </summary>
    /// <param name="seenSeries"></param>
    /// <param name="libraryId"></param>
    public async Task<IList<Series>> RemoveSeriesNotInList(IList<ParsedSeries> seenSeries, int libraryId)
    {
        if (seenSeries.Count == 0) return Array.Empty<Series>();

        var ids = new List<int>();
        foreach (var parsedSeries in seenSeries)
        {
            try
            {
                var seriesId = await _context.Series
                    .Where(s => s.Format == parsedSeries.Format && s.NormalizedName == parsedSeries.NormalizedName &&
                                s.LibraryId == libraryId)
                    .Select(s => s.Id)
                    .SingleOrDefaultAsync();
                if (seriesId > 0)
                {
                    ids.Add(seriesId);
                }
            }
            catch (Exception)
            {
                // This is due to v0.5.6 introducing bugs where we could have multiple series get duplicated and no way to delete them
                // This here will delete the 2nd one as the first is the one to likely be used.
                var sId = _context.Series
                    .Where(s => s.Format == parsedSeries.Format && s.NormalizedName == parsedSeries.NormalizedName &&
                                s.LibraryId == libraryId)
                    .Select(s => s.Id)
                    .OrderBy(s => s)
                    .Last();
                if (sId > 0)
                {
                    ids.Add(sId);
                }
            }
        }

        var seriesToRemove = await _context.Series
            .Where(s => s.LibraryId == libraryId)
            .Where(s => !ids.Contains(s.Id))
            .ToListAsync();

        // If the series to remove has Relation (related series), we must manually unlink due to the DB not being
        // setup correctly (if this is not done, a foreign key constraint will be thrown)

        foreach (var sr in seriesToRemove)
        {
            sr.Relations = new List<SeriesRelation>();
            Update(sr);
        }

        _context.Series.RemoveRange(seriesToRemove);

        return seriesToRemove;
    }

    public async Task<PagedList<SeriesDto>> GetHighlyRated(int userId, int libraryId, UserParams userParams)
    {
        var libraryIds = GetLibraryIdsForUser(userId, libraryId);
        var usersSeriesIds = GetSeriesIdsForLibraryIds(libraryIds);
        var distinctSeriesIdsWithHighRating = _context.AppUserRating
            .Where(s => usersSeriesIds.Contains(s.SeriesId) && s.Rating > 4)
            .Select(p => p.SeriesId)
            .Distinct();

        var query = _context.Series
            .Where(s => distinctSeriesIdsWithHighRating.Contains(s.Id))
            .AsSplitQuery()
            .OrderByDescending(s => _context.AppUserRating.Where(r => r.SeriesId == s.Id).Select(r => r.Rating).Average())
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider);

        return await PagedList<SeriesDto>.CreateAsync(query, userParams.PageNumber, userParams.PageSize);
    }


    public async Task<PagedList<SeriesDto>> GetQuickReads(int userId, int libraryId, UserParams userParams)
    {
        var libraryIds = GetLibraryIdsForUser(userId, libraryId);
        var usersSeriesIds = GetSeriesIdsForLibraryIds(libraryIds);
        var distinctSeriesIdsWithProgress = _context.AppUserProgresses
            .Where(s => usersSeriesIds.Contains(s.SeriesId))
            .Select(p => p.SeriesId)
            .Distinct();


        var query = _context.Series
            .Where(s => (
                (s.Pages / ReaderService.AvgPagesPerMinute / 60 < 10 && s.Format != MangaFormat.Epub)
                || (s.WordCount * ReaderService.AvgWordsPerHour < 10 && s.Format == MangaFormat.Epub))
                    && !distinctSeriesIdsWithProgress.Contains(s.Id) &&
                         usersSeriesIds.Contains(s.Id))
            .Where(s => s.Metadata.PublicationStatus != PublicationStatus.OnGoing)
            .AsSplitQuery()
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider);


        return await PagedList<SeriesDto>.CreateAsync(query, userParams.PageNumber, userParams.PageSize);
    }

    public async Task<PagedList<SeriesDto>> GetQuickCatchupReads(int userId, int libraryId, UserParams userParams)
    {
        var libraryIds = GetLibraryIdsForUser(userId, libraryId);
        var usersSeriesIds = GetSeriesIdsForLibraryIds(libraryIds);
        var distinctSeriesIdsWithProgress = _context.AppUserProgresses
            .Where(s => usersSeriesIds.Contains(s.SeriesId))
            .Select(p => p.SeriesId)
            .Distinct();


        var query = _context.Series
            .Where(s => (
                            (s.Pages / ReaderService.AvgPagesPerMinute / 60 < 10 && s.Format != MangaFormat.Epub)
                             || (s.WordCount * ReaderService.AvgWordsPerHour < 10 && s.Format == MangaFormat.Epub))
                        && !distinctSeriesIdsWithProgress.Contains(s.Id) &&
                        usersSeriesIds.Contains(s.Id))
            .Where(s => s.Metadata.PublicationStatus == PublicationStatus.OnGoing)
            .AsSplitQuery()
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider);


        return await PagedList<SeriesDto>.CreateAsync(query, userParams.PageNumber, userParams.PageSize);
    }

    /// <summary>
    /// Returns all library ids for a user
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="libraryId">0 for no library filter</param>
    /// <returns></returns>
    private IQueryable<int> GetLibraryIdsForUser(int userId, int libraryId = 0)
    {
        var query = _context.AppUser
            .AsSplitQuery()
            .AsNoTracking()
            .Where(u => u.Id == userId);

        if (libraryId == 0)
        {
            return query.SelectMany(l => l.Libraries.Select(lib => lib.Id));
        }

        return query.SelectMany(l =>
            l.Libraries.Where(lib => lib.Id == libraryId).Select(lib => lib.Id));
    }

    public async Task<RelatedSeriesDto> GetRelatedSeries(int userId, int seriesId)
    {
        var libraryIds = GetLibraryIdsForUser(userId);
        var usersSeriesIds = GetSeriesIdsForLibraryIds(libraryIds);
        var userRating = await GetUserAgeRestriction(userId);

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
            Parent = await _context.Series
                .SelectMany(s =>
                    s.RelationOf.Where(r => r.TargetSeriesId == seriesId
                                             && usersSeriesIds.Contains(r.TargetSeriesId)
                                             && r.RelationKind != RelationKind.Prequel
                                             && r.RelationKind != RelationKind.Sequel)
                        .Select(sr => sr.Series))
                .RestrictAgainstAgeRestriction(userRating)
                .AsSplitQuery()
                .AsNoTracking()
                .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
                .ToListAsync()
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
            .SelectMany(u => u.Libraries.Select(l => new {LibraryId = l.Id, LibraryType = l.Type}))
            .Select(l => l.LibraryId)
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
                ChapterNumber = c.Number,
                ChapterRange = c.Range,
                IsSpecial = c.IsSpecial,
                VolumeNumber = c.Volume.Number,
                ChapterTitle = c.Title,
                AgeRating = c.Volume.Series.Metadata.AgeRating
            })
            .AsSplitQuery()
            .Where(c => c.Created >= withinLastWeek && libraryIds.Contains(c.LibraryId))
            .AsEnumerable();
    }

    public async Task<PagedList<SeriesDto>> GetWantToReadForUserAsync(int userId, UserParams userParams, FilterDto filter)
    {
        var libraryIds = GetLibraryIdsForUser(userId);
        var query = _context.AppUser
            .Where(user => user.Id == userId)
            .SelectMany(u => u.WantToRead)
            .Where(s => libraryIds.Contains(s.LibraryId))
            .AsSplitQuery()
            .AsNoTracking();

        var filteredQuery = await CreateFilteredSearchQueryable(userId, 0, filter, query);

        return await PagedList<SeriesDto>.CreateAsync(filteredQuery.ProjectTo<SeriesDto>(_mapper.ConfigurationProvider), userParams.PageNumber, userParams.PageSize);
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
                Format = s.Format,
                LibraryRoots = s.Library.Folders.Select(f => f.Path)
            }).ToListAsync();

        var map = new Dictionary<string, IList<SeriesModified>>();
        foreach (var series in info)
        {
            if (!map.ContainsKey(series.FolderPath))
            {
                map.Add(series.FolderPath, new List<SeriesModified>()
                {
                    series
                });
            }
            else
            {
                map[series.FolderPath].Add(series);
            }

        }

        return map;
    }

    /// <summary>
    /// Returns the highest Age Rating for a list of Series
    /// </summary>
    /// <param name="seriesIds"></param>
    /// <returns></returns>
    public async Task<AgeRating> GetMaxAgeRatingFromSeriesAsync(IEnumerable<int> seriesIds)
    {
        return await _context.Series
            .Where(s => seriesIds.Contains(s.Id))
            .Include(s => s.Metadata)
            .Select(s => s.Metadata.AgeRating)
            .OrderBy(s => s)
            .LastOrDefaultAsync();
    }

    private static IQueryable<Series> AddIncludesToQuery(IQueryable<Series> query, SeriesIncludes includeFlags)
    {
        if (includeFlags.HasFlag(SeriesIncludes.Library))
        {
            query = query.Include(u => u.Library);
        }

        if (includeFlags.HasFlag(SeriesIncludes.Related))
        {
            query = query.Include(u => u.Relations);
        }

        if (includeFlags.HasFlag(SeriesIncludes.Metadata))
        {
            query = query.Include(u => u.Metadata);
        }

        if (includeFlags.HasFlag(SeriesIncludes.Volumes))
        {
            query = query.Include(u => u.Volumes);
        }


        return query;
    }
}
