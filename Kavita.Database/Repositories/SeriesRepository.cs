using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Kavita.API.Repositories;
using Kavita.API.Services.Helpers;
using Kavita.API.Services.Plus;
using Kavita.API.Services.Reading;
using Kavita.Common.Extensions;
using Kavita.Common.Helpers;
using Kavita.Database.Converters;
using Kavita.Database.Extensions;
using Kavita.Database.Extensions.Filters;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Collection;
using Kavita.Models.DTOs.Dashboard;
using Kavita.Models.DTOs.Filtering;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.DTOs.Filtering.v2.SortFields;
using Kavita.Models.DTOs.Filtering.v2.SortOptions;
using Kavita.Models.DTOs.KavitaPlus.Metadata;
using Kavita.Models.DTOs.Metadata;
using Kavita.Models.DTOs.Person;
using Kavita.Models.DTOs.Reader;
using Kavita.Models.DTOs.ReadingLists;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.DTOs.Search;
using Kavita.Models.DTOs.SeriesDetail;
using Kavita.Models.DTOs.Settings;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Metadata;
using Kavita.Models.Entities.User;
using Kavita.Models.Extensions;
using Kavita.Models.Misc;
using Kavita.Models.Parser;
using Microsoft.EntityFrameworkCore;


namespace Kavita.Database.Repositories;

public class SeriesRepository(DataContext context, IMapper mapper) : ISeriesRepository
{
    private readonly Regex _yearRegex = new(@"\d{4}", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

    public void Add(Series series)
    {
        context.Series.Add(series);
    }

    public void Attach(SeriesRelation relation)
    {
        context.SeriesRelation.Attach(relation);
    }

    public void Attach(ExternalSeriesMetadata metadata)
    {
        context.ExternalSeriesMetadata.Attach(metadata);
    }

    public void Update(Series series)
    {
        context.Entry(series).State = EntityState.Modified;
    }

    public void Update(SeriesMetadata seriesMetadata)
    {
        context.Entry(seriesMetadata).State = EntityState.Modified;
    }

    public void Remove(Series series)
    {
        context.Series.Remove(series);
    }

    public void Remove(IEnumerable<Series> series)
    {
        context.Series.RemoveRange(series);
    }

    /// <summary>
    /// Returns if a series name and format exists already in a library
    /// </summary>
    /// <param name="name">Name of series</param>
    /// <param name="libraryId"></param>
    /// <param name="format">Format of series</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<bool> DoesSeriesNameExistInLibrary(string name, int libraryId, MangaFormat format,
        CancellationToken ct = default)
    {
        return await context.Series
            .AsNoTracking()
            .Where(s => s.LibraryId == libraryId && s.Name.Equals(name) && s.Format == format)
            .AnyAsync(ct);
    }


    public async Task<IEnumerable<Series>> GetSeriesForLibraryIdAsync(int libraryId,
        SeriesIncludes includes = SeriesIncludes.None, CancellationToken ct = default)
    {
        return await context.Series
            .Where(s => s.LibraryId == libraryId)
            .Includes(includes)
            .OrderBy(s => s.SortName.ToLower())
            .ToListAsync(ct);
    }

    /// <summary>
    /// Used for <see cref="ScannerService"/> to
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="userParams"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<PagedList<Series>> GetFullSeriesForLibraryIdAsync(int libraryId, UserParams userParams,
        CancellationToken ct = default)
    {
#nullable  disable
        var query = context.Series
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

        return await PagedList<Series>.CreateAsync(query, userParams.PageNumber, userParams.PageSize, ct);
    }

    /// <summary>
    /// This is a heavy call. Returns all entities down to Files and Library and Series Metadata.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<Series?> GetFullSeriesForSeriesIdAsync(int seriesId, CancellationToken ct = default)
    {
#nullable  disable
        return await context.Series
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
            .SingleOrDefaultAsync(ct);
#nullable  enable
    }

    /// <summary>
    /// Gets all series
    /// </summary>
    /// <param name="libraryId">Restricts to just one library</param>
    /// <param name="userId"></param>
    /// <param name="userParams"></param>
    /// <param name="filter"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [Obsolete("Use GetSeriesDtoForLibraryIdAsync")]
    public async Task<PagedList<SeriesDto>> GetSeriesDtoForLibraryIdAsync(int libraryId, int userId,
        UserParams userParams, FilterDto filter, CancellationToken ct = default)
    {
        var query = await CreateFilteredSearchQueryable(userId, libraryId, filter, QueryContext.None, ct);

        var retSeries = query
            .ProjectToWithProgress<Series, SeriesDto>(mapper, userId)
            .AsSplitQuery()
            .AsNoTracking();

        return await PagedList<SeriesDto>.CreateAsync(retSeries, userParams.PageNumber, userParams.PageSize, ct);
    }

    private async Task<List<int>> GetUserLibrariesForFilteredQuery(int libraryId, int userId, QueryContext queryContext, CancellationToken ct = default)
    {
        if (libraryId == 0)
        {
            return await context.Library.GetUserLibraries(userId, queryContext).ToListAsync(ct);
        }

        return [libraryId];
    }

    public async Task<SearchResultGroupDto> SearchSeries(int userId, bool isAdmin, IList<int> libraryIds,
        string searchQuery, bool includeChapterAndFiles = true, CancellationToken ct = default)
    {
        const int maxRecords = 15;
        var searchQueryNormalized = searchQuery.ToNormalized();
        var userRating = await context.AppUser.GetUserAgeRestriction(userId, ct: ct);

        var justYear = _yearRegex.Match(searchQuery).Value;
        var hasYearInQuery = !string.IsNullOrEmpty(justYear);
        var yearComparison = hasYearInQuery ? int.Parse(justYear) : 0;


        var baseSeriesQuery = context.Series
            .Where(s => libraryIds.Contains(s.LibraryId))
            .RestrictAgainstAgeRestriction(userRating);

        #region Independent Queries
        var librariesTask = context.Library
            .Search(searchQuery, userId, libraryIds)
            .Take(maxRecords)
            .OrderBy(l => l.Name.ToLower())
            .ProjectTo<LibraryDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);

        var annotationsTask = context.AppUserAnnotation
            .Where(a => a.AppUserId == userId &&
                        (EF.Functions.Like(a.Comment, $"%{searchQueryNormalized}%") ||
                         EF.Functions.Like(a.Context, $"%{searchQueryNormalized}%")))
            .Take(maxRecords)
            .OrderBy(l => l.CreatedUtc)
            .ProjectTo<AnnotationDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);
        #endregion

        var seriesTask = baseSeriesQuery
            .Where(s => EF.Functions.Like(s.Name, $"%{searchQuery}%")
                        || (s.OriginalName != null && EF.Functions.Like(s.OriginalName, $"%{searchQuery}%"))
                        || (s.LocalizedName != null && EF.Functions.Like(s.LocalizedName, $"%{searchQuery}%"))
                        || EF.Functions.Like(s.NormalizedName, $"%{searchQueryNormalized}%")
                        || (hasYearInQuery && s.Metadata.ReleaseYear == yearComparison))
            .OrderBy(s => s.SortName!.Length)
            .ThenBy(s => s.SortName!.ToLower())
            .Take(maxRecords)
            .ProjectTo<SearchResultDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);

        var readingListsTask = context.ReadingList
            .Search(searchQuery, userId, userRating)
            .Take(maxRecords)
            .ProjectTo<ReadingListDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);

        var collectionsTask = context.AppUserCollection
            .Search(searchQuery, userId, userRating)
            .Take(maxRecords)
            .OrderBy(c => c.NormalizedTitle)
            .ProjectTo<AppUserCollectionDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);

        var bookmarksTask = context.AppUserBookmark
            .Where(b => b.AppUserId == userId)
            .Where(b => libraryIds.Contains(b.Series.LibraryId))
            .Where(b => EF.Functions.Like(b.Series.Name, $"%{searchQuery}%") ||
                        (b.Series.OriginalName != null && EF.Functions.Like(b.Series.OriginalName, $"%{searchQuery}%")) ||
                        (b.Series.LocalizedName != null && EF.Functions.Like(b.Series.LocalizedName, $"%{searchQuery}%")))
            .GroupBy(b => new { b.SeriesId, b.Series.LibraryId, b.Series.Name, b.Series.LocalizedName, b.Series.NormalizedName })
            .OrderBy(g => g.Key.NormalizedName.Length)
            .ThenBy(g => g.Key.NormalizedName)
            .Select(g => new BookmarkSearchResultDto
            {
                SeriesName = g.Key.Name,
                LocalizedSeriesName = g.Key.LocalizedName,
                LibraryId = g.Key.LibraryId,
                SeriesId = g.Key.SeriesId,
                ChapterId = g.First().ChapterId,
                VolumeId = g.First().VolumeId
            })
            .Take(maxRecords)
            .ToListAsync(ct);

        var seriesIdsSubquery = baseSeriesQuery.Select(s => s.Id);

        var personsTask = context.Person
            .Where(p => context.SeriesMetadataPeople
                .Any(smp => smp.PersonId == p.Id &&
                            seriesIdsSubquery.Contains(smp.SeriesMetadata.SeriesId) &&
                            (EF.Functions.Like(p.NormalizedName, $"%{searchQueryNormalized}%")
                             || p.Aliases.Any(a => EF.Functions.Like(a.NormalizedAlias, $"%{searchQueryNormalized}%"))
                            )))
            .OrderBy(p => p.NormalizedName.Length)
            .ThenBy(p => p.NormalizedName)
            .Take(maxRecords)
            .ProjectTo<PersonDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);

        var genresTask = context.Genre
            .Where(g => context.SeriesMetadata
                            .Any(sm => seriesIdsSubquery.Contains(sm.SeriesId) &&
                                       sm.Genres.Any(sg => sg.Id == g.Id)) &&
                        EF.Functions.Like(g.NormalizedTitle, $"%{searchQueryNormalized}%"))
            .Take(maxRecords)
            .ProjectTo<GenreTagDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);

        var tagsTask = context.Tag
            .Where(t => context.SeriesMetadata
                            .Any(sm => seriesIdsSubquery.Contains(sm.SeriesId) &&
                                       sm.Tags.Any(st => st.Id == t.Id)) &&
                        EF.Functions.Like(t.NormalizedTitle, $"%{searchQueryNormalized}%"))
            .Take(maxRecords)
            .ProjectTo<TagDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);

        // Run separate DB queries in parallel
        await Task.WhenAll(
            librariesTask, annotationsTask, seriesTask, readingListsTask,
            collectionsTask, bookmarksTask, personsTask, genresTask, tagsTask);

        var result = new SearchResultGroupDto
        {
            Libraries = await librariesTask,
            Annotations = await annotationsTask,
            Series = await seriesTask,
            ReadingLists = await readingListsTask,
            Collections = await collectionsTask,
            Bookmarks = await bookmarksTask,
            Persons = await personsTask,
            Genres = await genresTask,
            Tags = await tagsTask,
            Files = [],
            Chapters = []
        };

        if (includeChapterAndFiles)
        {
            // Use EXISTS subquery pattern instead of loading IDs
            var chaptersQuery = context.Chapter
                .Where(c => c.Volume.Series.LibraryId > 0 && // Ensure navigation works
                            libraryIds.Contains(c.Volume.Series.LibraryId))
                .Where(c => EF.Functions.Like(c.TitleName, $"%{searchQuery}%")
                            || EF.Functions.Like(c.ISBN, $"%{searchQuery}%")
                            || EF.Functions.Like(c.Range, $"%{searchQuery}%"));

            // Apply age restriction via series
            chaptersQuery = chaptersQuery
                .Where(c => baseSeriesQuery.Any(s => s.Id == c.Volume.SeriesId));

            result.Chapters = await chaptersQuery
                .OrderBy(c => c.TitleName.Length)
                .ThenBy(c => c.TitleName)
                .Take(maxRecords)
                .ProjectTo<ChapterDto>(mapper.ConfigurationProvider)
                .ToListAsync(ct);

            if (isAdmin)
            {
                result.Files = await context.MangaFile
                    .Where(f => EF.Functions.Like(f.FilePath, $"%{searchQuery}%"))
                    .Where(f => libraryIds.Contains(f.Chapter.Volume.Series.LibraryId))
                    .Where(f => baseSeriesQuery.Any(s => s.Id == f.Chapter.Volume.SeriesId))
                    .OrderBy(f => f.FilePath)
                    .Take(maxRecords)
                    .ProjectTo<MangaFileDto>(mapper.ConfigurationProvider)
                    .ToListAsync(ct);
            }
        }

        return result;
    }

    /// <summary>
    /// Includes Progress for the user
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<SeriesDto?> GetSeriesDtoByIdAsync(int seriesId, int userId, CancellationToken ct = default)
    {
        var series = await context.Series
            .Where(x => x.Id == seriesId)
            .ProjectToWithProgress<Series, SeriesDto>(mapper, userId)
            .SingleOrDefaultAsync(ct);

        return series ?? null;
    }

    /// <summary>
    /// Returns Volumes, Metadata (Incl Genres and People), and Collection Tags
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="includes"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<Series?> GetSeriesByIdAsync(int seriesId,
        SeriesIncludes includes = SeriesIncludes.Metadata | SeriesIncludes.Volumes, CancellationToken ct = default)
    {
        return await context.Series
            .Where(s => s.Id == seriesId)
            .Includes(includes)
            .SingleOrDefaultAsync(ct);
    }

    /// <summary>
    /// Returns Full Series including all external links
    /// </summary>
    /// <param name="seriesIds"></param>
    /// <param name="fullSeries">Include all the includes or just the Series</param>
    /// <returns></returns>
    public async Task<IList<Series>> GetSeriesByIdsAsync(IList<int> seriesIds, bool fullSeries = true, CancellationToken ct = default)
    {
        var query = context.Series
            .Where(s => seriesIds.Contains(s.Id))
            .AsSplitQuery();

        if (!fullSeries) return await query.ToListAsync(ct);

        return await query
            .Include(s => s.Volumes)
            .ThenInclude(v => v.Chapters)
            .ThenInclude(c => c.ExternalRatings)
            .Include(s => s.Volumes)
            .ThenInclude(v => v.Chapters)
            .ThenInclude(c => c.ExternalReviews)
            .Include(s => s.Relations)
            .Include(s => s.Metadata)

            .Include(s => s.ExternalSeriesMetadata)

            .Include(s => s.ExternalSeriesMetadata)
            .ThenInclude(e => e.ExternalRatings)
            .Include(s => s.ExternalSeriesMetadata)
            .ThenInclude(e => e.ExternalReviews)
            .Include(s => s.ExternalSeriesMetadata)
            .ThenInclude(e => e.ExternalRecommendations)
            .ToListAsync(ct);
    }

    public async Task<IList<SeriesDto>> GetSeriesDtoByIdsAsync(IEnumerable<int> seriesIds, AppUser user,
        CancellationToken ct = default)
    {
        var allowedLibraries = await context.Library
            .Where(library => library.AppUsers.Any(x => x.Id == user.Id))
            .Select(l => l.Id)
            .ToListAsync(ct);
        var restriction = new AgeRestriction()
        {
            AgeRating = user.AgeRestriction,
            IncludeUnknowns = user.AgeRestrictionIncludeUnknowns
        };
        return await context.Series
            .Include(s => s.Metadata)
            .Where(s => seriesIds.Contains(s.Id) && allowedLibraries.Contains(s.LibraryId))
            .RestrictAgainstAgeRestriction(restriction)
            .AsSplitQuery()
            .ProjectToWithProgress<Series, SeriesDto>(mapper, user.Id)
            .ToListAsync(ct);
    }

    public async Task<int[]> GetChapterIdsForSeriesAsync(IList<int> seriesIds, CancellationToken ct = default)
    {
        var volumes = await context.Volume
            .Where(v => seriesIds.Contains(v.SeriesId))
            .Include(v => v.Chapters)
            .AsSplitQuery()
            .ToListAsync(ct);

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
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<IDictionary<int, IList<int>>> GetChapterIdWithSeriesIdForSeriesAsync(int[] seriesIds,
        CancellationToken ct = default)
    {
        var volumes = await context.Volume
            .Where(v => seriesIds.Contains(v.SeriesId))
            .Include(v => v.Chapters)
            .AsSplitQuery()
            .ToListAsync(ct);

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

    public async Task<long> GetFilesizeAsync(int seriesId, CancellationToken ct = default)
    {
        return await context.Volume
            .Where(v => v.SeriesId == seriesId)
            .SumAsync(v => v.Chapters.Sum(c => c.Files.Sum(f => f.Bytes)), cancellationToken: ct);
    }

    public async Task<Dictionary<int, long>> GetFilesizesAsync(IList<int> seriesIds, CancellationToken ct = default)
    {
        return await seriesIds.BatchToDictionaryAsync(50, batch =>
            context.Volume
                .Where(v => batch.Contains(v.SeriesId))
                .GroupBy(v => v.SeriesId)
                .Select(g => new
                {
                    SeriesId = g.Key,
                    TotalBytes = g.SelectMany(v => v.Chapters)
                        .SelectMany(c => c.Files)
                        .Sum(f => f.Bytes)
                })
                .ToDictionaryAsync(x => x.SeriesId, x => x.TotalBytes, cancellationToken: ct));
    }

    public async Task<IList<SeriesMetadataDto>> GetSeriesMetadataForIds(IEnumerable<int> seriesIds,
        CancellationToken ct = default)
    {
        return await context.SeriesMetadata
            .Where(metadata => seriesIds.Contains(metadata.SeriesId))
            .Include(m => m.Genres.OrderBy(g => g.NormalizedTitle))
            .Include(m => m.Tags.OrderBy(g => g.NormalizedTitle))
            .Include(m => m.People)
            .ThenInclude(p => p.Person)
            .AsNoTracking()
            .ProjectTo<SeriesMetadataDto>(mapper.ConfigurationProvider)
            .AsSplitQuery()
            .ToListAsync(ct);
    }

    /// <summary>
    /// Returns custom images only
    /// </summary>
    /// <remarks>If customOnly, this will not include any volumes/chapters</remarks>
    /// <returns></returns>
    public async Task<IList<Series>> GetAllWithCoversInDifferentEncoding(EncodeFormat encodeFormat,
        bool customOnly = true, CancellationToken ct = default)
    {
        var extension = encodeFormat.GetExtension();
        var prefix = "series{0}".Replace("0", string.Empty); // default: This actually depends on ImageService#GetSeriesFormat
        var query = context.Series
            .Where(c => !string.IsNullOrEmpty(c.CoverImage)
                        && !c.CoverImage.EndsWith(extension)
                        && (!customOnly || c.CoverImage.StartsWith(prefix)))
            .AsSplitQuery();

        if (!customOnly)
        {
            query = query.Include(s => s.Volumes)
                .ThenInclude(v => v.Chapters);
        }

        return await query.ToListAsync(ct);
    }

    public async Task<PagedList<SeriesDto>> GetSeriesDtoForLibraryIdV2Async(int userId, UserParams userParams,
        FilterV2Dto filterDto, QueryContext queryContext = QueryContext.None, CancellationToken ct = default)
    {
        var query = await CreateFilteredSearchQueryableV2(userId, filterDto, queryContext, ct: ct);

        var retSeries = query.ProjectToWithProgress<Series, SeriesDto>(mapper, userId);

        return await PagedList<SeriesDto>.CreateAsync(retSeries, userParams.PageNumber, userParams.PageSize, ct);
    }

    public async Task<PlusSeriesRequestDto?> GetPlusSeriesDto(int seriesId, CancellationToken ct = default)
    {

        // I need to check Weblinks when AniListId/MalId is already set in ExternalSeries
        // Updating stale data should prioritize ExternalSeriesMetadata before Weblinks, to prioritize prior matches
        var result = await context.Series
            .Where(s => s.Id == seriesId)
            .Include(s => s.ExternalSeriesMetadata)
            .Select(series => new PlusSeriesRequestDto()
            {
                MediaFormat = series.Library.Type.ConvertToPlusMediaFormat(series.Format),
                SeriesName = series.Name,
                AltSeriesName = series.LocalizedName,
                AniListId = series.ExternalSeriesMetadata.AniListId != 0
                    ? series.ExternalSeriesMetadata.AniListId
                    : WeblinkParser.GetAniListId(series.Metadata.WebLinks),
                MalId = series.ExternalSeriesMetadata.MalId != 0
                    ? series.ExternalSeriesMetadata.MalId
                    : WeblinkParser.GetMalId(series.Metadata.WebLinks),
                CbrId = series.ExternalSeriesMetadata.CbrId,
                GoogleBooksId = !string.IsNullOrEmpty(series.ExternalSeriesMetadata.GoogleBooksId)
                    ? series.ExternalSeriesMetadata.GoogleBooksId
                    : WeblinkParser.GetGoogleBooksId(series.Metadata.WebLinks),
                MangaDexId = WeblinkParser.GetMangaDexId(series.Metadata.WebLinks),
                VolumeCount = series.Volumes.Count,
                ChapterCount = series.Volumes.SelectMany(v => v.Chapters).Count(c => !c.IsSpecial),
                Year = series.Metadata.ReleaseYear
            })
            .FirstOrDefaultAsync(ct);

        return result;
    }

    public async Task<string?> GetSeriesCoverImageAsync(int seriesId, CancellationToken ct = default)
    {
        return await context.Series
            .Where(s => s.Id == seriesId)
            .Select(s => s.CoverImage)
            .SingleOrDefaultAsync(ct);
    }


    /// <summary>
    /// Returns a list of Series that were added, ordered by Created desc
    /// </summary>
    /// <param name="libraryId">Library to restrict to, if 0, will apply to all libraries</param>
    /// <param name="userId"></param>
    /// <param name="userParams">Contains pagination information</param>
    /// <param name="filter">Optional filter on query</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [Obsolete("Use GetRecentlyAddedV2")]
    public async Task<PagedList<SeriesDto>> GetRecentlyAdded(int libraryId, int userId, UserParams userParams,
        FilterDto filter, CancellationToken ct = default)
    {
        var query = await CreateFilteredSearchQueryable(userId, libraryId, filter, QueryContext.Dashboard, ct);

        var retSeries = query
            .OrderByDescending(s => s.Created)
            .ProjectToWithProgress<Series, SeriesDto>(mapper, userId)
            .AsSplitQuery()
            .AsNoTracking();

        return await PagedList<SeriesDto>.CreateAsync(retSeries, userParams.PageNumber, userParams.PageSize, ct);
    }

    public async Task<PagedList<SeriesDto>> GetRecentlyAddedV2(int userId, UserParams userParams, FilterV2Dto filter,
        CancellationToken ct = default)
    {
        var query = await CreateFilteredSearchQueryableV2(userId, filter, QueryContext.Dashboard, ct: ct);

        var retSeries = query
            .OrderByDescending(s => s.Created)
            .ProjectToWithProgress<Series, SeriesDto>(mapper, userId)
            .AsSplitQuery()
            .AsNoTracking();

        return await PagedList<SeriesDto>.CreateAsync(retSeries, userParams.PageNumber, userParams.PageSize, ct);
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
            seriesIds = context.Series
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
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<PagedList<SeriesDto>> GetOnDeck(int userId, int libraryId, UserParams userParams,
        FilterDto? filter, CancellationToken ct = default)
    {
        var settings = await context.ServerSetting
            .Select(x => x)
            .AsNoTracking()
            .ToListAsync(ct);
        var serverSettings = mapper.Map<ServerSettingDto>(settings);

        var cutoffProgressPoint = DateTime.Now - TimeSpan.FromDays(serverSettings.OnDeckProgressDays);
        var cutoffLastAddedPoint = DateTime.Now - TimeSpan.FromDays(serverSettings.OnDeckUpdateDays);

        var libraryIds = context.AppUser.GetLibraryIdsForUser(userId, libraryId, QueryContext.Dashboard)
            .Where(id => libraryId == 0 || id == libraryId);
        var usersSeriesIds = GetSeriesIdsForLibraryIds(libraryIds);

        // Don't allow any series the user has explicitly removed
        var onDeckRemovals = context.AppUserOnDeckRemoval
            .Where(d => d.AppUserId == userId)
            .Select(d => d.SeriesId)
            .AsEnumerable();

        var query = context.Series
            .Where(s => usersSeriesIds.Contains(s.Id))
            .Where(s => !onDeckRemovals.Contains(s.Id))
            .Select(s => new
            {
                Series = s,
                PagesRead = context.AppUserProgresses.Where(p => p.SeriesId == s.Id && p.AppUserId == userId)
                    .Sum(s1 => s1.PagesRead),
                LatestReadDate = context.AppUserProgresses
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
            .ProjectToWithProgress<Series, SeriesDto>(mapper, userId)
            .AsSplitQuery()
            .AsNoTracking();

        return await PagedList<SeriesDto>.CreateAsync(query, userParams.PageNumber, userParams.PageSize, ct);
    }

    private async Task<IQueryable<Series>> CreateFilteredSearchQueryable(int userId, int libraryId, FilterDto filter, QueryContext queryContext, CancellationToken ct = default)
    {
        // NOTE: Why do we even have libraryId when the filter has the actual libraryIds?
        var userLibraries = await GetUserLibrariesForFilteredQuery(libraryId, userId, queryContext, ct);
        var userRating = await context.AppUser.GetUserAgeRestriction(userId, ct: ct);
        var onlyParentSeries = await context.AppUserPreferences.Where(u => u.AppUserId == userId)
            .Select(u => u.CollapseSeriesRelationships)
            .SingleOrDefaultAsync(ct);

        var formats = ExtractFilters(libraryId, userId, filter, ref userLibraries,
            out var allPeopleIds, out var hasPeopleFilter, out var hasGenresFilter,
            out var hasCollectionTagFilter, out var hasRatingFilter, out var hasProgressFilter,
            out var seriesIds, out var hasAgeRating, out var hasTagsFilter, out var hasLanguageFilter,
            out var hasPublicationFilter, out var hasSeriesNameFilter, out var hasReleaseYearMinFilter, out var hasReleaseYearMaxFilter);

        IList<int> collectionSeries = [];
        if (hasCollectionTagFilter)
        {
            collectionSeries = await context.AppUserCollection
                .Where(uc => uc.Promoted || uc.AppUserId == userId)
                .Where(uc => filter.CollectionTags.Contains(uc.Id))
                .SelectMany(uc => uc.Items)
                .RestrictAgainstAgeRestriction(userRating)
                .Select(s => s.Id)
                .Distinct()
                .ToListAsync(ct);
        }


        var query = context.Series
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
            .HasFormat(filter.Formats is {Count: > 0}, FilterComparison.Contains, filter.Formats!)
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
        filter.SortOptions ??= new SeriesSortOptionDto()
        {
            IsAscending = true,
            SortField = SeriesSortField.SortName
        };

        query = filter.SortOptions.SortField switch
        {
            SeriesSortField.SortName => query.DoOrderBy(s => s.SortName.ToLower(), filter.SortOptions),
            SeriesSortField.CreatedDate => query.DoOrderBy(s => s.Created, filter.SortOptions),
            SeriesSortField.LastModifiedDate => query.DoOrderBy(s => s.LastModified, filter.SortOptions),
            SeriesSortField.LastChapterAdded => query.DoOrderBy(s => s.LastChapterAdded, filter.SortOptions),
            SeriesSortField.TimeToRead => query.DoOrderBy(s => s.AvgHoursToRead, filter.SortOptions),
            SeriesSortField.ReleaseYear => query.DoOrderBy(s => s.Metadata.ReleaseYear, filter.SortOptions),
            SeriesSortField.ReadProgress => query.DoOrderBy(s => s.Progress.Where(p => p.SeriesId == s.Id).Select(p => p.LastModified).Max(), filter.SortOptions),
            SeriesSortField.AverageRating => query.DoOrderBy(s => s.ExternalSeriesMetadata.ExternalRatings
                .Where(p => p.SeriesId == s.Id).Average(p => p.AverageScore), filter.SortOptions),
            _ => query
        };

        return query.AsSplitQuery();
    }

    private async Task<IQueryable<Series>> CreateFilteredSearchQueryableV2(int userId, FilterV2Dto filter,
        QueryContext queryContext, IQueryable<Series>? query = null, CancellationToken ct = default)
    {
        var userLibraries = await GetUserLibrariesForFilteredQuery(0, userId, queryContext, ct);
        var allLibraryCount = await context.Library.CountAsync(ct);
        var userRating = await context.AppUser.GetUserAgeRestriction(userId, ct: ct);
        var onlyParentSeries = await context.AppUserPreferences.Where(u => u.AppUserId == userId)
            .Select(u => u.CollapseSeriesRelationships)
            .SingleOrDefaultAsync(ct);

        query ??= context.Series
            .AsNoTracking();

        // When the user has no access, just return instantly
        if (userLibraries.Count == 0)
        {
            return query.Where(s => false);
        }

        // First setup any FilterField.Libraries in the statements, as these don't have any traditional query statements applied here
        query = ApplyLibraryFilter(filter, query);

        query = ApplyWantToReadFilter(filter, query, userId);

        query = await ApplyCollectionFilter(filter, query, userId, userRating, ct);




        query = FilterQueryBuilder.Apply(filter, query,
            (stmt, q) => BuildFilterGroup(userId, stmt, q));

        query = query
            .WhereIf(allLibraryCount != userLibraries.Count && userLibraries.Count > 0, s => userLibraries.Contains(s.LibraryId))
            .WhereIf(onlyParentSeries, s =>
                s.RelationOf.Count == 0 ||
                s.RelationOf.All(p => p.RelationKind == RelationKind.Prequel))
            .RestrictAgainstAgeRestriction(userRating);


        return query
                .Sort(userId, filter.SortOptions)
                .AsSplitQuery()
                .ApplyLimit(filter.LimitTo);
    }

    private async Task<IQueryable<Series>> ApplyCollectionFilter(FilterV2Dto filter, IQueryable<Series> query,
        int userId, AgeRestriction userRating, CancellationToken ct = default)
    {
        var collectionStmt = filter.Statements.FirstOrDefault(stmt => stmt.Field == SeriesFilterField.CollectionTags);
        if (collectionStmt == null) return query;

        var value = (IList<int>) SeriesFilterFieldValueConverter.ConvertValue(collectionStmt.Field, collectionStmt.Value);

        if (value.Count == 0)
        {
            return query;
        }

        var collectionSeries = await context.AppUserCollection
            .Where(uc => uc.Promoted || uc.AppUserId == userId)
            .Where(uc => value.Contains(uc.Id))
            .SelectMany(uc => uc.Items)
            .RestrictAgainstAgeRestriction(userRating)
            .Select(s => s.Id)
            .Distinct()
            .ToListAsync(ct);

        if (collectionStmt.Comparison != FilterComparison.MustContains)
            return query.HasCollectionTags(true, collectionStmt.Comparison, value, collectionSeries);

        var collectionSeriesTasks = value.Select(async collectionId =>
        {
            return await context.AppUserCollection
                .Where(uc => uc.Promoted || uc.AppUserId == userId)
                .Where(uc => uc.Id == collectionId)
                .SelectMany(uc => uc.Items)
                .RestrictAgainstAgeRestriction(userRating)
                .Select(s => s.Id)
                .ToListAsync(ct);
        });

        var collectionSeriesLists = await Task.WhenAll(collectionSeriesTasks);

        // Find the common series among all collections
        var commonSeries = collectionSeriesLists.Aggregate((common, next) => common.Intersect(next).ToList());

        // Filter the original query based on the common series
        return query.Where(s => commonSeries.Contains(s.Id));
    }

    private IQueryable<Series> ApplyWantToReadFilter(FilterV2Dto filter, IQueryable<Series> query, int userId)
    {
        var wantToReadStmt = filter.Statements.FirstOrDefault(stmt => stmt.Field == SeriesFilterField.WantToRead);
        if (wantToReadStmt == null) return query;

        var seriesIds = context.AppUser.Where(u => u.Id == userId)
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
            foreach (var stmt in filter.Statements.Where(stmt => stmt.Field == SeriesFilterField.Libraries))
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
            filter.Statements = filter.Statements.Where(stmt => stmt.Field != SeriesFilterField.Libraries).ToList();
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

    private static IQueryable<Series> BuildFilterGroup(int userId, FilterStatementDto statement, IQueryable<Series> query)
    {

        var value = SeriesFilterFieldValueConverter.ConvertValue(statement.Field, statement.Value);
        return statement.Field switch
        {
            SeriesFilterField.Summary => query.HasSummary(true, statement.Comparison, (string) value),
            SeriesFilterField.SeriesName => query.HasName(true, statement.Comparison, (string) value),
            SeriesFilterField.Path => query.HasPath(true, statement.Comparison, (string) value),
            SeriesFilterField.FilePath => query.HasFilePath(true, statement.Comparison, (string) value),
            SeriesFilterField.PublicationStatus => query.HasPublicationStatus(true, statement.Comparison,
                (IList<PublicationStatus>) value),
            SeriesFilterField.Languages => query.HasLanguage(true, statement.Comparison, (IList<string>) value),
            SeriesFilterField.AgeRating => query.HasAgeRating(true, statement.Comparison, (IList<AgeRating>) value),
            SeriesFilterField.UserRating => query.HasRating(true, statement.Comparison, (float) value , userId),
            SeriesFilterField.Tags => query.HasTags(true, statement.Comparison, (IList<int>) value),
            SeriesFilterField.Translators => query.HasPeople(true, statement.Comparison, (IList<int>) value, PersonRole.Translator),
            SeriesFilterField.Characters => query.HasPeople(true, statement.Comparison, (IList<int>) value, PersonRole.Character),
            SeriesFilterField.Publisher => query.HasPeople(true, statement.Comparison, (IList<int>) value, PersonRole.Publisher),
            SeriesFilterField.Editor => query.HasPeople(true, statement.Comparison, (IList<int>) value, PersonRole.Editor),
            SeriesFilterField.CoverArtist => query.HasPeople(true, statement.Comparison, (IList<int>) value, PersonRole.CoverArtist),
            SeriesFilterField.Letterer => query.HasPeople(true, statement.Comparison, (IList<int>) value, PersonRole.Letterer),
            SeriesFilterField.Colorist => query.HasPeople(true, statement.Comparison, (IList<int>) value, PersonRole.Inker),
            SeriesFilterField.Inker => query.HasPeople(true, statement.Comparison, (IList<int>) value, PersonRole.Inker),
            SeriesFilterField.Imprint => query.HasPeople(true, statement.Comparison, (IList<int>) value, PersonRole.Imprint),
            SeriesFilterField.Team => query.HasPeople(true, statement.Comparison, (IList<int>) value, PersonRole.Team),
            SeriesFilterField.Location => query.HasPeople(true, statement.Comparison, (IList<int>) value, PersonRole.Location),
            SeriesFilterField.Penciller => query.HasPeople(true, statement.Comparison, (IList<int>) value, PersonRole.Penciller),
            SeriesFilterField.Writers => query.HasPeople(true, statement.Comparison, (IList<int>) value, PersonRole.Writer),
            SeriesFilterField.Genres => query.HasGenre(true, statement.Comparison, (IList<int>) value),
            SeriesFilterField.CollectionTags =>
                // This is handled in the code before this as it's handled in a more general, combined manner
                query,
            SeriesFilterField.Libraries =>
                // This is handled in the code before this as it's handled in a more general, combined manner
                query,
            SeriesFilterField.WantToRead =>
                // This is handled in the higher level of code as it's more general
                query,
            SeriesFilterField.ReadProgress => query.HasReadingProgress(true, statement.Comparison, (float) value, userId),
            SeriesFilterField.Formats => query.HasFormat(true, statement.Comparison, (IList<MangaFormat>) value),
            SeriesFilterField.ReleaseYear => query.HasReleaseYear(true, statement.Comparison, (int) value),
            SeriesFilterField.ReadTime => query.HasAverageReadTime(true, statement.Comparison, (int) value),
            SeriesFilterField.ReadingDate => query.HasReadingDate(true, statement.Comparison, (DateTime) value, userId),
            SeriesFilterField.ReadLast => query.HasReadLast(true, statement.Comparison, (int) value, userId),
            SeriesFilterField.AverageRating => query.HasAverageRating(true, statement.Comparison, (float) value),
            SeriesFilterField.FileSize => query.HasFileSize(true, statement.Comparison, (long) value),
            _ => throw new ArgumentOutOfRangeException(nameof(statement.Field), $"Unexpected value for field: {statement.Field}"),
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

    public async Task<SeriesMetadataDto?> GetSeriesMetadata(int seriesId, CancellationToken ct = default)
    {
        return await context.SeriesMetadata
            .Where(metadata => metadata.SeriesId == seriesId)
            .Include(m => m.Genres.OrderBy(g => g.NormalizedTitle))
            .Include(m => m.Tags.OrderBy(g => g.NormalizedTitle))
            .Include(m => m.People)
            .ThenInclude(p => p.Person)
            .AsNoTracking()
            .ProjectTo<SeriesMetadataDto>(mapper.ConfigurationProvider)
            .AsSplitQuery()
            .SingleOrDefaultAsync(ct);
    }

    public async Task<PagedList<SeriesDto>> GetSeriesDtoForCollectionAsync(int collectionId, int userId,
        UserParams userParams, CancellationToken ct = default)
    {
        var userLibraries = context.Library.GetUserLibraries(userId);

        var query =  context.AppUserCollection
            .Where(s => s.Id == collectionId)
            .Include(c => c.Items)
            .SelectMany(c => c.Items.Where(s => userLibraries.Contains(s.LibraryId)))
            .OrderBy(s => s.LibraryId)
            .ThenBy(s => s.SortName.ToLower())
            .ProjectToWithProgress<Series, SeriesDto>(mapper, userId)
            .AsSplitQuery();

        return await PagedList<SeriesDto>.CreateAsync(query, userParams.PageNumber, userParams.PageSize, ct);
    }

    public async Task<IList<MangaFile>> GetFilesForSeries(int seriesId, CancellationToken ct = default)
    {
        return await context.Volume
            .Where(v => v.SeriesId == seriesId)
            .Include(v => v.Chapters)
            .ThenInclude(c => c.Files)
            .SelectMany(v => v.Chapters.SelectMany(c => c.Files))
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<SeriesDto>> GetSeriesDtoForIdsAsync(IEnumerable<int> seriesIds, int userId,
        CancellationToken ct = default)
    {
        var allowedLibraries = context.Library
            .Include(l => l.AppUsers)
            .Where(library => library.AppUsers.Any(x => x.Id == userId))
            .AsSplitQuery()
            .Select(l => l.Id);
        var userRating = await context.AppUser.GetUserAgeRestriction(userId, ct: ct);

        return await context.Series
            .RestrictAgainstAgeRestriction(userRating)
            .Where(s => seriesIds.Contains(s.Id) && allowedLibraries.Contains(s.LibraryId))
            .OrderBy(s => s.SortName.ToLower())
            .ProjectTo<SeriesDto>(mapper.ConfigurationProvider)
            .AsNoTracking()
            .AsSplitQuery()
            .ToListAsync(ct);
    }

    public async Task<IList<string>> GetAllCoverImagesAsync(CancellationToken ct = default)
    {
        return (await context.Series
            .Select(s => s.CoverImage)
            .Where(t => !string.IsNullOrEmpty(t))
            .ToListAsync(ct))!;
    }

    public async Task<IEnumerable<string>> GetLockedCoverImagesAsync(CancellationToken ct = default)
    {
        return (await context.Series
            .Where(s => s.CoverImageLocked && !string.IsNullOrEmpty(s.CoverImage))
            .Select(s => s.CoverImage)
            .ToListAsync(ct))!;
    }

    /// <summary>
    /// Returns the number of series for a given library (or all libraries if libraryId is 0)
    /// </summary>
    /// <param name="libraryId">Defaults to 0, library to restrict count to</param>
    /// <returns></returns>
    private async Task<int> GetSeriesCount(int libraryId = 0, CancellationToken ct = default)
    {
        if (libraryId > 0)
        {
            return await context.Series
                .Where(s => s.LibraryId == libraryId)
                .CountAsync(ct);
        }
        return await context.Series.CountAsync(ct);
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

    public async Task<Chunk> GetChunkInfo(int libraryId = 0, CancellationToken ct = default)
    {
        var (totalSeries, chunkSize) = await GetChunkSize(libraryId);

        if (totalSeries == 0) return new Chunk
        {
            TotalChunks = 0,
            TotalSize = 0,
            ChunkSize = 0
        };

        var totalChunks = Math.Max((int) Math.Ceiling((totalSeries * 1.0) / chunkSize), 1);

        return new Chunk
        {
            TotalSize = totalSeries,
            ChunkSize = chunkSize,
            TotalChunks = totalChunks
        };
    }

    /// <summary>
    /// Return recently updated series, regardless of read progress, and group the number of volume or chapters added.
    /// </summary>
    /// <remarks>This provides 2 levels of pagination. Fetching the individual chapters only looks at 3000. Then when performing grouping
    /// in memory, we stop after 30 series. </remarks>
    /// <param name="userId">Used to ensure user has access to libraries</param>
    /// <param name="userParams">Page size and offset</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<IList<GroupedSeriesDto>> GetRecentlyUpdatedSeries(int userId, UserParams? userParams,
        CancellationToken ct = default)
    {
        userParams ??= UserParams.Default;

        var userRating = await context.AppUser.GetUserAgeRestriction(userId, ct: ct);

        var items = await GetRecentlyAddedChaptersQuery(userId, ct);
        if (userRating.AgeRating != AgeRating.NotApplicable)
        {
            items = items.RestrictAgainstAgeRestriction(userRating);
        }

        var index = 0;
        var seriesMap = new Dictionary<int, GroupedSeriesDto>();
        var toSkip = (userParams.PageNumber - 1) * userParams.PageSize;
        var skipped = new HashSet<int>();

        foreach (var item in items)
        {
            if (seriesMap.Keys.Count == userParams.PageSize) break;

            if (item.SeriesName == null) continue;

            if (skipped.Count < toSkip)
            {
                skipped.Add(item.SeriesId);
                continue;
            }

            if (seriesMap.TryGetValue(item.SeriesId, out var value))
            {
                value.Count += 1;
            }
            else
            {
                seriesMap[item.SeriesId] = new GroupedSeriesDto()
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

        return seriesMap.Values.ToList();
    }

    public async Task<IEnumerable<SeriesDto>> GetSeriesForRelationKind(int userId, int seriesId, RelationKind kind,
        CancellationToken ct = default)
    {
        var libraryIds = context.AppUser.GetLibraryIdsForUser(userId);
        var userRating = await context.AppUser.GetUserAgeRestriction(userId, ct: ct);

        var usersSeriesIds = context.Series
            .Where(s => libraryIds.Contains(s.LibraryId))
            .RestrictAgainstAgeRestriction(userRating)
            .Select(s => s.Id);

        var targetSeries = context.SeriesRelation
            .Where(sr =>
                sr.SeriesId == seriesId && sr.RelationKind == kind && usersSeriesIds.Contains(sr.TargetSeriesId))
            .Include(sr => sr.TargetSeries)
            .AsSplitQuery()
            .AsNoTracking()
            .Select(sr => sr.TargetSeriesId);

        return await context.Series
            .Where(s => targetSeries.Contains(s.Id))
            .RestrictAgainstAgeRestriction(userRating)
            .AsSplitQuery()
            .AsNoTracking()
            .ProjectToWithProgress<Series, SeriesDto>(mapper.ConfigurationProvider, userId)
            .ToListAsync(ct);
    }

    public async Task<PagedList<SeriesDto>> GetMoreIn(int userId, int libraryId, int genreId, UserParams userParams,
        CancellationToken ct = default)
    {
        var libraryIds = context.AppUser.GetLibraryIdsForUser(userId, libraryId, QueryContext.Dashboard)
            .Where(id => libraryId == 0 || id == libraryId);
        var usersSeriesIds = GetSeriesIdsForLibraryIds(libraryIds);

        var userRating = await context.AppUser.GetUserAgeRestriction(userId, ct: ct);
        // Because this can be called from an API, we need to provide an additional check if the genre has anything the
        // user with age restrictions can access

        var query = context.Series
            .Where(s => s.Metadata.Genres.Select(g => g.Id).Contains(genreId))
            .Where(s => usersSeriesIds.Contains(s.Id))
            .RestrictAgainstAgeRestriction(userRating)
            .AsSplitQuery()
            .ProjectToWithProgress<Series, SeriesDto>(mapper, userId);


        return await PagedList<SeriesDto>.CreateAsync(query, userParams.PageNumber, userParams.PageSize, ct);
    }

    /// <summary>
    /// Returns a list of Series that the user Has fully read
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="libraryId"></param>
    /// <param name="userParams"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<PagedList<SeriesDto>> GetRediscover(int userId, int libraryId, UserParams userParams,
        CancellationToken ct = default)
    {
        var libraryIds = context.AppUser.GetLibraryIdsForUser(userId, libraryId, QueryContext.Recommended)
            .Where(id => libraryId == 0 || id == libraryId);
        var usersSeriesIds = GetSeriesIdsForLibraryIds(libraryIds);
        var distinctSeriesIdsWithProgress = context.AppUserProgresses
            .Where(s => usersSeriesIds.Contains(s.SeriesId))
            .Select(p => p.SeriesId)
            .Distinct();

        var query = context.Series
            .Where(s => distinctSeriesIdsWithProgress.Contains(s.Id) &&
                        context.AppUserProgresses.Where(s1 => s1.SeriesId == s.Id && s1.AppUserId == userId)
                            .Sum(s1 => s1.PagesRead) >= s.Pages)
            .AsSplitQuery()
            .ProjectTo<SeriesDto>(mapper.ConfigurationProvider);

        return await PagedList<SeriesDto>.CreateAsync(query, userParams.PageNumber, userParams.PageSize, ct);
    }

    public async Task<SeriesDto?> GetSeriesForMangaFile(int mangaFileId, int userId, CancellationToken ct = default)
    {
        var libraryIds = context.AppUser.GetLibraryIdsForUser(userId, 0, QueryContext.Search);
        var userRating = await context.AppUser.GetUserAgeRestriction(userId, ct: ct);

        return await context.MangaFile
            .Where(m => m.Id == mangaFileId)
            .AsSplitQuery()
            .Select(f => f.Chapter)
            .Select(c => c.Volume)
            .Select(v => v.Series)
            .Where(s => libraryIds.Contains(s.LibraryId))
            .RestrictAgainstAgeRestriction(userRating)
            .ProjectTo<SeriesDto>(mapper.ConfigurationProvider)
            .SingleOrDefaultAsync(ct);
    }

    public async Task<SeriesDto?> GetSeriesForChapter(int chapterId, int userId, CancellationToken ct = default)
    {
        var libraryIds = context.AppUser.GetLibraryIdsForUser(userId);
        var userRating = await context.AppUser.GetUserAgeRestriction(userId, ct: ct);
        return await context.Chapter
            .Where(m => m.Id == chapterId)
            .AsSplitQuery()
            .Select(c => c.Volume)
            .Select(v => v.Series)
            .Where(s => libraryIds.Contains(s.LibraryId))
            .RestrictAgainstAgeRestriction(userRating)
            .ProjectTo<SeriesDto>(mapper.ConfigurationProvider)
            .SingleOrDefaultAsync(ct);
    }

    /// <summary>
    /// Return a Series by Folder path. Null if not found.
    /// </summary>
    /// <param name="folder">This will be normalized in the query and checked against FolderPath and LowestFolderPath</param>
    /// <param name="includes">Additional relationships to include with the base query</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<Series?> GetSeriesByFolderPath(string folder, SeriesIncludes includes = SeriesIncludes.None,
        CancellationToken ct = default)
    {
        var normalized = folder.NormalizePath();
        if (string.IsNullOrEmpty(normalized)) return null;

        return await context.Series
            .Where(s => (!string.IsNullOrEmpty(s.FolderPath) && s.FolderPath.Equals(normalized) || (!string.IsNullOrEmpty(s.LowestFolderPath) && s.LowestFolderPath.Equals(normalized))))
            .Includes(includes)
            .SingleOrDefaultAsync(ct);
    }

    public async Task<Series?> GetSeriesThatContainsLowestFolderPath(string path,
        SeriesIncludes includes = SeriesIncludes.None, CancellationToken ct = default)
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
        var normalized = directoryPath.NormalizePath();
        if (string.IsNullOrEmpty(normalized)) return null;

        normalized = normalized.TrimEnd('/');

        return await context.Series
            .Where(s => !string.IsNullOrEmpty(s.LowestFolderPath) && EF.Functions.Like(normalized, s.LowestFolderPath + "%"))
            .Includes(includes)
            .SingleOrDefaultAsync(ct);
    }

    public async Task<IEnumerable<Series>> GetAllSeriesByNameAsync(IList<string> normalizedNames,
        int userId, SeriesIncludes includes = SeriesIncludes.None, CancellationToken ct = default)
    {
        var libraryIds = context.Library.GetUserLibraries(userId);
        var userRating = await context.AppUser.GetUserAgeRestriction(userId, ct: ct);

        return await context.Series
            .Where(s => normalizedNames.Contains(s.NormalizedName) ||
                        normalizedNames.Contains(s.NormalizedLocalizedName))
            .Where(s => libraryIds.Contains(s.LibraryId))
            .RestrictAgainstAgeRestriction(userRating)
            .Includes(includes)
            .ToListAsync(ct);
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
    /// <param name="ct"></param>
    /// <returns></returns>
    public Task<Series?> GetFullSeriesByAnyName(string seriesName, string localizedName, int libraryId,
        MangaFormat format, bool withFullIncludes = true, CancellationToken ct = default)
    {
        var normalizedSeries = seriesName.ToNormalized();
        var normalizedLocalized = localizedName.ToNormalized();
        var query = context.Series
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
            return query.SingleOrDefaultAsync(ct);
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
        return query.SingleOrDefaultAsync(ct);

#nullable enable
    }

    public async Task<Series?> GetSeriesByAnyName(string seriesName, string localizedName, IList<MangaFormat> formats,
        int userId, int? aniListId = null, SeriesIncludes includes = SeriesIncludes.None, CancellationToken ct = default)
    {
        var libraryIds = context.AppUser.GetLibraryIdsForUser(userId);
        var normalizedSeries = seriesName.ToNormalized();
        var normalizedLocalized = localizedName.ToNormalized();

        var query = context.Series
            .Where(s => libraryIds.Contains(s.LibraryId))
            .Where(s => formats.Contains(s.Format));

        if (aniListId is > 0)
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
            .FirstOrDefaultAsync(ct);
    }


    public async Task<Series?> GetSeriesByAnyName(IList<string> names, IList<MangaFormat> formats,
        int userId, int? aniListId = null, SeriesIncludes includes = SeriesIncludes.None, CancellationToken ct = default)
    {
        var libraryIds = context.AppUser.GetLibraryIdsForUser(userId);
        names = names.Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
        var normalizedNames = names.Select(s => s.ToNormalized()).ToList();


        var query = context.Series
            .Where(s => libraryIds.Contains(s.LibraryId))
            .Where(s => formats.Contains(s.Format));

        if (aniListId is > 0)
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
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IList<Series>> GetAllSeriesByAnyName(string seriesName, string localizedName, int libraryId,
        MangaFormat format, CancellationToken ct = default)
    {
        var normalizedSeries = seriesName.ToNormalized();
        var normalizedLocalized = localizedName.ToNormalized();
        return await context.Series
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
            .ToListAsync(ct);
    }


    /// <summary>
    /// Removes series that are not in the seenSeries list. Does not commit.
    /// </summary>
    /// <param name="seenSeries"></param>
    /// <param name="libraryId"></param>
    /// <param name="ct"></param>
    public async Task<IList<Series>> RemoveSeriesNotInList(IList<ParsedSeries> seenSeries, int libraryId,
        CancellationToken ct = default)
    {
        if (!seenSeries.Any()) return Array.Empty<Series>();

        // Get all series from DB in one go, based on libraryId
        var dbSeries = await context.Series
            .Where(s => s.LibraryId == libraryId)
            .ToListAsync(ct);

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
        context.Series.RemoveRange(seriesToRemove);

        return seriesToRemove;
    }

    public async Task<PagedList<SeriesDto>> GetHighlyRated(int userId, int libraryId, UserParams userParams,
        CancellationToken ct = default)
    {
        var libraryIds = context.AppUser.GetLibraryIdsForUser(userId, libraryId, QueryContext.Recommended)
            .Where(id => libraryId == 0 || id == libraryId);
        var usersSeriesIds = GetSeriesIdsForLibraryIds(libraryIds);
        var distinctSeriesIdsWithHighRating = context.AppUserRating
            .Where(s => usersSeriesIds.Contains(s.SeriesId) && s.Rating > 4)
            .Select(p => p.SeriesId)
            .Distinct();
        var userRating = await context.AppUser.GetUserAgeRestriction(userId, ct: ct);

        var query = context.Series
            .Where(s => distinctSeriesIdsWithHighRating.Contains(s.Id))
            .RestrictAgainstAgeRestriction(userRating)
            .AsSplitQuery()
            .OrderByDescending(s => context.AppUserRating.Where(r => r.SeriesId == s.Id).Select(r => r.Rating).Average())
            .ProjectToWithProgress<Series, SeriesDto>(mapper, userId);

        return await PagedList<SeriesDto>.CreateAsync(query, userParams.PageNumber, userParams.PageSize, ct);
    }


    public async Task<PagedList<SeriesDto>> GetQuickReads(int userId, int libraryId, UserParams userParams,
        CancellationToken ct = default)
    {
        var libraryIds = context.AppUser.GetLibraryIdsForUser(userId, libraryId, QueryContext.Recommended)
            .Where(id => libraryId == 0 || id == libraryId);
        var usersSeriesIds = GetSeriesIdsForLibraryIds(libraryIds);
        var distinctSeriesIdsWithProgress = context.AppUserProgresses
            .Where(s => usersSeriesIds.Contains(s.SeriesId))
            .Select(p => p.SeriesId)
            .Distinct();
        var userRating = await context.AppUser.GetUserAgeRestriction(userId, ct: ct);


        var query = context.Series
            .Where(s => (
                            (s.Pages / IReaderService.AvgPagesPerMinute / 60 < 10 && s.Format != MangaFormat.Epub)
                            || (s.WordCount * IReaderService.AvgWordsPerHour < 10 && s.Format == MangaFormat.Epub))
                        && !distinctSeriesIdsWithProgress.Contains(s.Id) &&
                        usersSeriesIds.Contains(s.Id))
            .Where(s => s.Metadata.PublicationStatus != PublicationStatus.OnGoing)
            .RestrictAgainstAgeRestriction(userRating)
            .AsSplitQuery()
            .ProjectTo<SeriesDto>(mapper.ConfigurationProvider);


        return await PagedList<SeriesDto>.CreateAsync(query, userParams.PageNumber, userParams.PageSize, ct);
    }

    public async Task<PagedList<SeriesDto>> GetQuickCatchupReads(int userId, int libraryId, UserParams userParams,
        CancellationToken ct = default)
    {
        var libraryIds = context.AppUser.GetLibraryIdsForUser(userId, libraryId, QueryContext.Dashboard)
            .Where(id => libraryId == 0 || id == libraryId);
        var usersSeriesIds = GetSeriesIdsForLibraryIds(libraryIds);
        var distinctSeriesIdsWithProgress = context.AppUserProgresses
            .Where(s => usersSeriesIds.Contains(s.SeriesId))
            .Select(p => p.SeriesId)
            .Distinct();

        var userRating = await context.AppUser.GetUserAgeRestriction(userId, ct: ct);


        var query = context.Series
            .Where(s => (
                            (s.Pages / IReaderService.AvgPagesPerMinute / 60 < 10 && s.Format != MangaFormat.Epub)
                            || (s.WordCount * IReaderService.AvgWordsPerHour < 10 && s.Format == MangaFormat.Epub))
                        && !distinctSeriesIdsWithProgress.Contains(s.Id) &&
                        usersSeriesIds.Contains(s.Id))
            .Where(s => s.Metadata.PublicationStatus == PublicationStatus.OnGoing)
            .RestrictAgainstAgeRestriction(userRating)
            .AsSplitQuery()
            .ProjectTo<SeriesDto>(mapper.ConfigurationProvider);


        return await PagedList<SeriesDto>.CreateAsync(query, userParams.PageNumber, userParams.PageSize, ct);
    }

    public async Task<RelatedSeriesDto> GetRelatedSeries(int userId, int seriesId, CancellationToken ct = default)
    {
        var libraryIds = context.Library.GetUserLibraries(userId);
        var usersSeriesIds = GetSeriesIdsForLibraryIds(libraryIds);
        var userRating = await context.AppUser.GetUserAgeRestriction(userId, ct: ct);

        return new RelatedSeriesDto()
        {
            SourceSeriesId = seriesId,
            Adaptations = await GetRelatedSeriesQuery(userId, seriesId, usersSeriesIds, RelationKind.Adaptation, userRating, ct),
            Characters = await GetRelatedSeriesQuery(userId, seriesId, usersSeriesIds, RelationKind.Character, userRating, ct),
            Prequels = await GetRelatedSeriesQuery(userId, seriesId, usersSeriesIds, RelationKind.Prequel, userRating, ct),
            Sequels = await GetRelatedSeriesQuery(userId, seriesId, usersSeriesIds, RelationKind.Sequel, userRating, ct),
            Contains = await GetRelatedSeriesQuery(userId, seriesId, usersSeriesIds, RelationKind.Contains, userRating, ct),
            SideStories = await GetRelatedSeriesQuery(userId, seriesId, usersSeriesIds, RelationKind.SideStory, userRating, ct),
            SpinOffs = await GetRelatedSeriesQuery(userId, seriesId, usersSeriesIds, RelationKind.SpinOff, userRating, ct),
            Others = await GetRelatedSeriesQuery(userId, seriesId, usersSeriesIds, RelationKind.Other, userRating, ct),
            AlternativeSettings = await GetRelatedSeriesQuery(userId, seriesId, usersSeriesIds, RelationKind.AlternativeSetting, userRating, ct),
            AlternativeVersions = await GetRelatedSeriesQuery(userId, seriesId, usersSeriesIds, RelationKind.AlternativeVersion, userRating, ct),
            Doujinshis = await GetRelatedSeriesQuery(userId, seriesId, usersSeriesIds, RelationKind.Doujinshi, userRating, ct),
            Annuals = await GetRelatedSeriesQuery(userId, seriesId, usersSeriesIds, RelationKind.Annual, userRating, ct),
            Parent = await context.SeriesRelation
                .Where(r => r.TargetSeriesId == seriesId
                            && usersSeriesIds.Contains(r.TargetSeriesId)
                            && r.RelationKind != RelationKind.Prequel
                            && r.RelationKind != RelationKind.Sequel
                            && r.RelationKind != RelationKind.Edition)
                .Select(sr => sr.Series)
                .RestrictAgainstAgeRestriction(userRating)
                .AsSplitQuery()
                .AsNoTracking()
                .ProjectToWithProgress<Series, SeriesDto>(mapper.ConfigurationProvider, userId)
                .ToListAsync(ct),
            Editions = await GetRelatedSeriesQuery(userId, seriesId, usersSeriesIds, RelationKind.Edition, userRating, ct)
        };
    }

    private IQueryable<int> GetSeriesIdsForLibraryIds(IQueryable<int> libraryIds)
    {
        return context.Series
            .Where(s => libraryIds.Contains(s.LibraryId))
            .Select(s => s.Id);
    }

    private async Task<IEnumerable<SeriesDto>> GetRelatedSeriesQuery(int userId, int seriesId, IEnumerable<int> usersSeriesIds,
        RelationKind kind, AgeRestriction userRating, CancellationToken ct = default)
    {
        return await context.Series.SelectMany(s =>
                s.Relations.Where(sr => sr.RelationKind == kind && sr.SeriesId == seriesId && usersSeriesIds.Contains(sr.TargetSeriesId))
                    .Select(sr => sr.TargetSeries))
            .RestrictAgainstAgeRestriction(userRating)
            .AsSplitQuery()
            .AsNoTracking()
            .ProjectToWithProgress<Series, SeriesDto>(mapper.ConfigurationProvider, userId)
            .ToListAsync(ct);
    }

    private async Task<IEnumerable<RecentlyAddedSeriesDto>> GetRecentlyAddedChaptersQuery(int userId, CancellationToken ct = default)
    {
        var libraryIds = await context.AppUser
            .Where(u => u.Id == userId)
            .SelectMany(u => u.Libraries)
            .Where(l => l.IncludeInDashboard)
            .Select(l => l.Id)
            .ToListAsync(ct);

        var withinLastWeek = DateTime.Now - TimeSpan.FromDays(12);
        return context.Chapter
            .Where(c => c.Created >= withinLastWeek).AsNoTracking()
            .Include(c => c.Volume)
            .ThenInclude(v => v.Series)
            .ThenInclude(s => s.Library)
            .OrderByDescending(c => c.Created)
            .Select(c => new RecentlyAddedSeriesDto
            {
                LibraryId = c.Volume.Series.LibraryId,
                LibraryType = c.Volume.Series.Library.Type,
                Created = c.Created,
                SeriesId = c.Volume.Series.Id,
                SeriesName = c.Volume.Series.Name,
                VolumeId = c.VolumeId,
                ChapterId = c.Id,
                Format = c.Volume.Series.Format,
                ChapterNumber = c.MinNumber + string.Empty, // default: Refactor this
                ChapterRange = c.Range, // default: Refactor this
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
    public async Task<PagedList<SeriesDto>> GetWantToReadForUserAsync(int userId, UserParams userParams,
        FilterDto filter, CancellationToken ct = default)
    {
        var libraryIds = await context.Library.GetUserLibraries(userId).ToListAsync(ct);
        var query = context.AppUser
            .Where(user => user.Id == userId)
            .SelectMany(u => u.WantToRead)
            .Where(s => libraryIds.Contains(s.Series.LibraryId))
            .Select(w => w.Series)
            .AsSplitQuery()
            .AsNoTracking();

        var filteredQuery = await CreateFilteredSearchQueryable(userId, 0, filter, query);

        return await PagedList<SeriesDto>.CreateAsync(filteredQuery.ProjectToWithProgress<Series, SeriesDto>(mapper, userId), userParams.PageNumber, userParams.PageSize, ct);
    }

    public async Task<PagedList<SeriesDto>> GetWantToReadForUserV2Async(int userId, UserParams userParams,
        FilterV2Dto filter, CancellationToken ct = default)
    {
        var libraryIds = await context.Library.GetUserLibraries(userId).ToListAsync(ct);
        var seriesIds = await context.AppUser
            .Where(user => user.Id == userId)
            .SelectMany(u => u.WantToRead)
            .Where(s => libraryIds.Contains(s.Series.LibraryId))
            .Select(w => w.Series.Id)
            .Distinct()
            .ToListAsync(ct);

        var query = await CreateFilteredSearchQueryableV2(userId, filter, QueryContext.None, ct: ct);

        // Apply the Want to Read filtering
        query = query.Where(s => seriesIds.Contains(s.Id));

        var retSeries = query
            .ProjectToWithProgress<Series, SeriesDto>(mapper, userId)
            .AsSplitQuery()
            .AsNoTracking();

        return await PagedList<SeriesDto>.CreateAsync(retSeries, userParams.PageNumber, userParams.PageSize, ct);
    }

    public async Task<IList<Series>> GetWantToReadForUserAsync(int userId, CancellationToken ct = default)
    {
        var libraryIds = await context.Library.GetUserLibraries(userId).ToListAsync(ct);
        return await context.AppUser
            .Where(user => user.Id == userId)
            .SelectMany(u => u.WantToRead)
            .Where(s => libraryIds.Contains(s.Series.LibraryId))
            .Select(w => w.Series)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(ct);
    }

    /// <summary>
    /// Uses multiple names to find a match against a series. If not, returns null.
    /// </summary>
    /// <remarks>This does not restrict to the user at all. That is handled at the API level.</remarks>
    public async Task<SeriesDto?> GetSeriesDtoByNamesAndMetadataIds(IEnumerable<string> names, LibraryType libraryType,
        string aniListUrl, string malUrl, CancellationToken ct = default)
    {
        var libraryIds = await context.Library
            .Where(lib => lib.Type == libraryType)
            .Select(l => l.Id)
            .ToListAsync(ct);

        var normalizedNames = names.Select(n => n.ToNormalized()).ToList();
        SeriesDto? result = null;
        if (!string.IsNullOrEmpty(aniListUrl) || !string.IsNullOrEmpty(malUrl))
        {
            // default: I can likely work AniList and MalIds from ExternalSeriesMetadata in here
            result =  await context.Series
                .Where(s => !string.IsNullOrEmpty(s.Metadata.WebLinks))
                .Where(s => libraryIds.Contains(s.Library.Id))
                .WhereIf(!string.IsNullOrEmpty(aniListUrl), s => s.Metadata.WebLinks.Contains(aniListUrl))
                .WhereIf(!string.IsNullOrEmpty(malUrl), s => s.Metadata.WebLinks.Contains(malUrl))
                .ProjectTo<SeriesDto>(mapper.ConfigurationProvider)
                .AsSplitQuery()
                .FirstOrDefaultAsync(ct);
        }

        if (result != null) return result;

        return await context.Series
            .Where(s => normalizedNames.Contains(s.NormalizedName) ||
                        normalizedNames.Contains(s.NormalizedLocalizedName))
            .Where(s => libraryIds.Contains(s.Library.Id))
            .ProjectTo<SeriesDto>(mapper.ConfigurationProvider)
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct); // Some users may have improperly configured libraries
    }

    public async Task<Series?> MatchSeries(ExternalSeriesDetailDto externalSeries, CancellationToken ct = default)
    {
        var libraryIds = await context.Library
            .Where(lib => externalSeries.PlusMediaFormat.ConvertToLibraryTypes().Contains(lib.Type))
            .Select(l => l.Id)
            .ToListAsync(ct);

        var normalizedNames = (externalSeries.Synonyms ?? Enumerable.Empty<string>())
            .Prepend(externalSeries.Name)
            .Select(n => n.ToNormalized())
            .ToList();

        var aniListWebLink = ScrobblingHelper.CreateUrl(ScrobblingHelper.AniListWeblinkWebsite, externalSeries.AniListId);
        var malWebLink = ScrobblingHelper.CreateUrl(ScrobblingHelper.MalWeblinkWebsite, externalSeries.MALId);

        Series? result = null;
        if (!string.IsNullOrEmpty(aniListWebLink) || !string.IsNullOrEmpty(malWebLink))
        {
            result = await context.Series
                .Where(s => !string.IsNullOrEmpty(s.Metadata.WebLinks))
                .Where(s => libraryIds.Contains(s.Library.Id))
                .WhereIf(!string.IsNullOrEmpty(aniListWebLink), s => s.Metadata.WebLinks.Contains(aniListWebLink))
                .WhereIf(!string.IsNullOrEmpty(malWebLink), s => s.Metadata.WebLinks.Contains(malWebLink))
                .Include(s => s.Metadata)
                .AsSplitQuery()
                .FirstOrDefaultAsync(ct);
        }

        if (result != null) return result;

        return await context.Series
            .Where(s => normalizedNames.Contains(s.NormalizedName) ||
                        normalizedNames.Contains(s.NormalizedLocalizedName))
            .Where(s => libraryIds.Contains(s.Library.Id))
            .AsSplitQuery()
            .Include(s => s.Metadata)
            .FirstOrDefaultAsync(ct); // Some users may have improperly configured libraries
    }

    /// <summary>
    /// Returns the Average rating for all users within Kavita instance
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="userId"></param>
    /// <param name="ct"></param>
    public async Task<int> GetAverageUserRating(int seriesId, int userId, CancellationToken ct = default)
    {
        // If there is 0 or 1 rating and that rating is you, return 0 back
        var countOfRatingsThatAreUser = await context.AppUserRating
            .Where(r => r.SeriesId == seriesId && r.HasBeenRated)
            .CountAsync(u => u.AppUserId == userId, cancellationToken: ct);
        if (countOfRatingsThatAreUser == 1)
        {
            return 0;
        }
        var avg = (await context.AppUserRating
            .Where(r => r.SeriesId == seriesId && r.HasBeenRated)
            .AverageAsync(r => (int?) r.Rating, cancellationToken: ct));
        return avg.HasValue ? (int) (avg.Value * 20) : 0;
    }

    public async Task RemoveFromOnDeck(int seriesId, int userId, CancellationToken ct = default)
    {
        var existingEntry = await context.AppUserOnDeckRemoval
            .Where(u => u.Id == userId && u.SeriesId == seriesId)
            .AnyAsync(ct);
        if (existingEntry) return;
        context.AppUserOnDeckRemoval.Add(new AppUserOnDeckRemoval()
        {
            SeriesId = seriesId,
            AppUserId = userId
        });
        await context.SaveChangesAsync(ct);
    }

    public async Task ClearOnDeckRemoval(int seriesId, int userId, CancellationToken ct = default)
    {
        var existingEntry = await context.AppUserOnDeckRemoval
            .Where(u => u.AppUserId == userId && u.SeriesId == seriesId)
            .FirstOrDefaultAsync(ct);
        if (existingEntry == null) return;
        context.AppUserOnDeckRemoval.Remove(existingEntry);
        await context.SaveChangesAsync(ct);
    }

    public async Task<bool> IsSeriesInWantToRead(int userId, int seriesId, CancellationToken ct = default)
    {
        var libraryIds = await context.Library.GetUserLibraries(userId).ToListAsync(ct);
        return await context.AppUser
            .Where(user => user.Id == userId)
            .SelectMany(u => u.WantToRead.Where(s => s.SeriesId == seriesId && libraryIds.Contains(s.Series.LibraryId)))
            .AsSplitQuery()
            .AsNoTracking()
            .AnyAsync(ct);
    }

    public async Task<IDictionary<string, IList<SeriesModified>>> GetFolderPathMap(int libraryId,
        CancellationToken ct = default)
    {
        var info = await context.Series
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
            .ToListAsync(ct);

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
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<AgeRating> GetMaxAgeRatingFromSeriesAsync(IEnumerable<int> seriesIds, CancellationToken ct = default)
    {
        var ret = await context.Series
            .Where(s => seriesIds.Contains(s.Id))
            .Include(s => s.Metadata)
            .Select(s => s.Metadata.AgeRating)
            .OrderBy(s => s)
            .LastOrDefaultAsync(ct);

        if (ret == null) return AgeRating.Unknown;
        return ret;
    }
}
