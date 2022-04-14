using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using API.Data.Scanner;
using API.DTOs;
using API.DTOs.CollectionTags;
using API.DTOs.Filtering;
using API.DTOs.Metadata;
using API.DTOs.ReadingLists;
using API.DTOs.Search;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Extensions;
using API.Helpers;
using API.Services.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Kavita.Common.Extensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

internal class RecentlyAddedSeries
{
    public int LibraryId { get; init; }
    public LibraryType LibraryType { get; init; }
    public DateTime Created { get; init; }
    public int SeriesId { get; init; }
    public string SeriesName { get; init; }
    public MangaFormat Format { get; init; }
    public int ChapterId { get; init; }
    public int VolumeId { get; init; }
    public string ChapterNumber { get; init; }
    public string ChapterRange { get; init; }
    public string ChapterTitle { get; init; }
    public bool IsSpecial { get; init; }
    public int VolumeNumber { get; init; }
}

public interface ISeriesRepository
{
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
    /// <param name="userParams"></param>
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
    Task<Series> GetSeriesByIdAsync(int seriesId);
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
    Task<IEnumerable<SeriesDto>> GetOnDeck(int userId, int libraryId, UserParams userParams, FilterDto filter, bool cutoffOnDate = true);
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
    Task<IList<AgeRatingDto>> GetAllAgeRatingsDtosForLibrariesAsync(List<int> libraryIds);
    Task<IList<LanguageDto>> GetAllLanguagesForLibrariesAsync(List<int> libraryIds);
    Task<IList<PublicationStatusDto>> GetAllPublicationStatusesDtosForLibrariesAsync(List<int> libraryIds);
    Task<IEnumerable<GroupedSeriesDto>> GetRecentlyUpdatedSeries(int userId, int pageSize = 30);
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

        var result = new SearchResultGroupDto();
        var searchQueryNormalized = Parser.Parser.Normalize(searchQuery);

        var seriesIds = _context.Series
            .Where(s => libraryIds.Contains(s.LibraryId))
            .Select(s => s.Id)
            .ToList();

        result.Libraries = await _context.Library
            .Where(l => libraryIds.Contains(l.Id))
            .Where(l => EF.Functions.Like(l.Name, $"%{searchQuery}%"))
            .OrderBy(l => l.Name)
            .AsSplitQuery()
            .ProjectTo<LibraryDto>(_mapper.ConfigurationProvider)
            .ToListAsync();

        var justYear = Regex.Match(searchQuery, @"\d{4}").Value;
        var hasYearInQuery = !string.IsNullOrEmpty(justYear);
        var yearComparison = hasYearInQuery ? int.Parse(justYear) : 0;

        result.Series = await _context.Series
            .Where(s => libraryIds.Contains(s.LibraryId))
            .Where(s => EF.Functions.Like(s.Name, $"%{searchQuery}%")
                        || EF.Functions.Like(s.OriginalName, $"%{searchQuery}%")
                        || EF.Functions.Like(s.LocalizedName, $"%{searchQuery}%")
                        || EF.Functions.Like(s.NormalizedName, $"%{searchQueryNormalized}%")
                        || (hasYearInQuery && s.Metadata.ReleaseYear == yearComparison))
            .Include(s => s.Library)
            .OrderBy(s => s.SortName)
            .AsNoTracking()
            .AsSplitQuery()
            .ProjectTo<SearchResultDto>(_mapper.ConfigurationProvider)
            .ToListAsync();


        result.ReadingLists = await _context.ReadingList
            .Where(rl => rl.AppUserId == userId || rl.Promoted)
            .Where(rl => EF.Functions.Like(rl.Title, $"%{searchQuery}%"))
            .AsSplitQuery()
            .ProjectTo<ReadingListDto>(_mapper.ConfigurationProvider)
            .ToListAsync();

        result.Collections =  await _context.CollectionTag
            .Where(s => EF.Functions.Like(s.Title, $"%{searchQuery}%")
                        || EF.Functions.Like(s.NormalizedTitle, $"%{searchQueryNormalized}%"))
            .Where(s => s.Promoted || isAdmin)
            .OrderBy(s => s.Title)
            .AsNoTracking()
            .OrderBy(c => c.NormalizedTitle)
            .ProjectTo<CollectionTagDto>(_mapper.ConfigurationProvider)
            .ToListAsync();

        result.Persons = await _context.SeriesMetadata
            .Where(sm => seriesIds.Contains(sm.SeriesId))
            .SelectMany(sm => sm.People.Where(t => EF.Functions.Like(t.Name, $"%{searchQuery}%")))
            .AsSplitQuery()
            .Distinct()
            .ProjectTo<PersonDto>(_mapper.ConfigurationProvider)
            .ToListAsync();

        result.Genres = await _context.SeriesMetadata
            .Where(sm => seriesIds.Contains(sm.SeriesId))
            .SelectMany(sm => sm.Genres.Where(t => EF.Functions.Like(t.Title, $"%{searchQuery}%")))
            .AsSplitQuery()
            .OrderBy(t => t.Title)
            .Distinct()
            .ProjectTo<GenreTagDto>(_mapper.ConfigurationProvider)
            .ToListAsync();

        result.Tags = await _context.SeriesMetadata
            .Where(sm => seriesIds.Contains(sm.SeriesId))
            .SelectMany(sm => sm.Tags.Where(t => EF.Functions.Like(t.Title, $"%{searchQuery}%")))
            .AsSplitQuery()
            .OrderBy(t => t.Title)
            .Distinct()
            .ProjectTo<TagDto>(_mapper.ConfigurationProvider)
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
    /// <returns></returns>
    public async Task<Series> GetSeriesByIdAsync(int seriesId)
    {
        return await _context.Series
            .Include(s => s.Volumes)
            .Include(s => s.Metadata)
            .ThenInclude(m => m.CollectionTags)
            .Include(s => s.Metadata)
            .ThenInclude(m => m.Genres)
            .Include(s => s.Metadata)
            .ThenInclude(m => m.People)
            .Where(s => s.Id == seriesId)
            .AsSplitQuery()
            .SingleOrDefaultAsync();
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
            .Where(s => seriesIds.Contains(s.Id))
            .AsSplitQuery()
            .ToListAsync();
    }

    public async Task<int[]> GetChapterIdsForSeriesAsync(IList<int> seriesIds)
    {
        var volumes = await _context.Volume
            .Where(v => seriesIds.Contains(v.SeriesId))
            .Include(v => v.Chapters)
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
            .ToListAsync();

        var userRatings = await _context.AppUserRating
            .Where(r => r.AppUserId == userId && series.Select(s => s.Id).Contains(r.SeriesId))
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
        out bool hasLanguageFilter, out bool hasPublicationFilter, out bool hasSeriesNameFilter)
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
    /// Returns Series that the user has some partial progress on. Sorts based on activity. Sort first by User progress, but if a series
    /// has been updated recently, bump it to the front.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="libraryId">Library to restrict to, if 0, will apply to all libraries</param>
    /// <param name="userParams">Pagination information</param>
    /// <param name="filter">Optional (default null) filter on query</param>
    /// <returns></returns>
    public async Task<IEnumerable<SeriesDto>> GetOnDeck(int userId, int libraryId, UserParams userParams, FilterDto filter, bool cutoffOnDate = true)
    {
        var query = (await CreateFilteredSearchQueryable(userId, libraryId, filter))
            .Join(_context.AppUserProgresses, s => s.Id, progress => progress.SeriesId, (s, progress) =>
                new
                {
                    Series = s,
                    PagesRead = _context.AppUserProgresses.Where(s1 => s1.SeriesId == s.Id && s1.AppUserId == userId)
                        .Sum(s1 => s1.PagesRead),
                    progress.AppUserId,
                    LastReadingProgress = _context.AppUserProgresses
                        .Where(p => p.Id == progress.Id && p.AppUserId == userId)
                        .Max(p => p.LastModified),
                    LastModified = _context.AppUserProgresses.Where(p => p.Id == progress.Id && p.AppUserId == userId).Max(p => p.LastModified),
                    s.LastChapterAdded
                });
        if (cutoffOnDate)
        {
            var cutoffProgressPoint = DateTime.Now - TimeSpan.FromDays(30);
            query = query.Where(d => d.LastReadingProgress >= cutoffProgressPoint || d.LastChapterAdded >= cutoffProgressPoint);
        }

        var retSeries = query.Where(s => s.AppUserId == userId
                                         && s.PagesRead > 0
                                         && s.PagesRead < s.Series.Pages)
            .OrderByDescending(s => s.LastChapterAdded)
            .ThenByDescending(s => s.LastReadingProgress)
            .Select(s => s.Series)
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
            .AsSplitQuery()
            .AsNoTracking();

        // Pagination does not work for this query as when we pull the data back, we get multiple rows of the same series. See controller for pagination code
        return await retSeries.ToListAsync();
    }

    private async Task<IQueryable<Series>> CreateFilteredSearchQueryable(int userId, int libraryId, FilterDto filter)
    {
        var userLibraries = await GetUserLibraries(libraryId, userId);
        var formats = ExtractFilters(libraryId, userId, filter, ref userLibraries,
            out var allPeopleIds, out var hasPeopleFilter, out var hasGenresFilter,
            out var hasCollectionTagFilter, out var hasRatingFilter, out var hasProgressFilter,
            out var seriesIds, out var hasAgeRating, out var hasTagsFilter, out var hasLanguageFilter, out var hasPublicationFilter, out var hasSeriesNameFilter);

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
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<SeriesDto>> GetSeriesDtoForIdsAsync(IEnumerable<int> seriesIds, int userId)
    {
        var allowedLibraries = _context.Library
            .Include(l => l.AppUsers)
            .Where(library => library.AppUsers.Any(x => x.Id == userId))
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
            .ToListAsync();
    }

    public async Task<IList<AgeRatingDto>> GetAllAgeRatingsDtosForLibrariesAsync(List<int> libraryIds)
    {
        return await _context.Series
            .Where(s => libraryIds.Contains(s.LibraryId))
            .Select(s => s.Metadata.AgeRating)
            .Distinct()
            .Select(s => new AgeRatingDto()
            {
                Value = s,
                Title = s.ToDescription()
            })
            .ToListAsync();
    }

    public async Task<IList<LanguageDto>> GetAllLanguagesForLibrariesAsync(List<int> libraryIds)
    {
        var ret = await _context.Series
            .Where(s => libraryIds.Contains(s.LibraryId))
            .Select(s => s.Metadata.Language)
            .AsNoTracking()
            .Distinct()
            .ToListAsync();

        return ret
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(s => new LanguageDto()
            {
                Title = CultureInfo.GetCultureInfo(s).DisplayName,
                IsoCode = s
            })
            .OrderBy(s => s.Title)
            .ToList();
    }

    public async Task<IList<PublicationStatusDto>> GetAllPublicationStatusesDtosForLibrariesAsync(List<int> libraryIds)
    {
        return await _context.Series
            .Where(s => libraryIds.Contains(s.LibraryId))
            .Select(s => s.Metadata.PublicationStatus)
            .Distinct()
            .Select(s => new PublicationStatusDto()
            {
                Value = s,
                Title = s.ToDescription()
            })
            .OrderBy(s => s.Title)
            .ToListAsync();
    }


    /// <summary>
    /// Return recently updated series, regardless of read progress, and group the number of volume or chapters added.
    /// </summary>
    /// <remarks>This provides 2 levels of pagination. Fetching the individual chapters only looks at 3000. Then when performing grouping
    /// in memory, we stop after 30 series. </remarks>
    /// <param name="userId">Used to ensure user has access to libraries</param>
    /// <returns></returns>
    public async Task<IEnumerable<GroupedSeriesDto>> GetRecentlyUpdatedSeries(int userId, int pageSize = 30)
    {
        var seriesMap = new Dictionary<string, GroupedSeriesDto>();
         var index = 0;
         foreach (var item in await GetRecentlyAddedChaptersQuery(userId))
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

    private async Task<IEnumerable<RecentlyAddedSeries>> GetRecentlyAddedChaptersQuery(int userId, int maxRecords = 3000)
    {
        var libraries = await _context.AppUser
            .Where(u => u.Id == userId)
            .SelectMany(u => u.Libraries.Select(l => new {LibraryId = l.Id, LibraryType = l.Type}))
            .ToListAsync();
        var libraryIds = libraries.Select(l => l.LibraryId).ToList();

        var withinLastWeek = DateTime.Now - TimeSpan.FromDays(12);
        var ret = _context.Chapter
            .Where(c => c.Created >= withinLastWeek)
            .AsNoTracking()
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
                ChapterTitle = c.Title
            })
            //.Take(maxRecords)
            .AsSplitQuery()
            .Where(c => c.Created >= withinLastWeek && libraryIds.Contains(c.LibraryId))
            .AsEnumerable();
        return ret;
    }
}
