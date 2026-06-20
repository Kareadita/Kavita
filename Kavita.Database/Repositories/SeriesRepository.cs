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
using Kavita.API.Services.Plus;
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
using Kavita.Models.DTOs.Filtering.v2.Requests;
using Kavita.Models.DTOs.Filtering.v2.SortFields;
using Kavita.Models.DTOs.Filtering.v2.SortOptions;
using Kavita.Models.DTOs.KavitaPlus.Metadata;
using Kavita.Models.DTOs.KavitaPlus.Scrobble;
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
    public async Task<bool> DoesSeriesNameExistInLibraryAsync(string name, int libraryId, MangaFormat format,
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

    private async Task<List<int>> GetUserLibrariesForFilteredQuery(int libraryId, int userId, QueryContext queryContext, CancellationToken ct = default)
    {
        if (libraryId == 0)
        {
            return await context.Library.GetUserLibraries(userId, queryContext).ToListAsync(ct);
        }

        return [libraryId];
    }

    public async Task<SearchResultGroupDto> SearchSeriesAsync(int userId, bool isAdmin, IList<int> libraryIds,
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
            .Where(s =>
                (EF.Functions.Like(s.Name, $"%{searchQuery}%")
                 || (s.OriginalName != null && EF.Functions.Like(s.OriginalName, $"%{searchQuery}%"))
                 || (s.LocalizedName != null && EF.Functions.Like(s.LocalizedName, $"%{searchQuery}%"))
                 || EF.Functions.Like(s.NormalizedName, $"%{searchQueryNormalized}%")))
            .WhereIf(hasYearInQuery, s =>
                s.Metadata.ReleaseYear == yearComparison
                || s.Name.Contains(justYear)
                || (s.OriginalName != null &&
                    s.OriginalName.Contains(justYear))
                || (s.LocalizedName != null &&
                    s.LocalizedName.Contains(justYear))
                || (s.NormalizedName != null &&
                    s.NormalizedName.Contains(justYear)))
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

    public async Task<IList<SeriesMetadataDto>> GetSeriesMetadataForIdsAsync(IEnumerable<int> seriesIds,
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
    public async Task<IList<Series>> GetAllWithCoversInDifferentEncodingAsync(EncodeFormat encodeFormat,
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

    public async Task<PagedList<SeriesDto>> GetSeriesDtoForLibraryIdAsync(int userId, UserParams userParams,
        SeriesFilterV2Dto seriesFilterDto, QueryContext queryContext = QueryContext.None, CancellationToken ct = default)
    {
        var query = await CreateFilteredSearchQueryableV2(userId, seriesFilterDto, queryContext, ct: ct);

        var retSeries = query.ProjectToWithProgress<Series, SeriesDto>(mapper, userId);

        return await PagedList<SeriesDto>.CreateAsync(retSeries, userParams.PageNumber, userParams.PageSize, ct);
    }

    public async Task<PlusSeriesRequestDto?> GetPlusSeriesDtoAsync(int seriesId, CancellationToken ct = default)
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
                // TODO: Refactor this to check from ExternalMetadataIds first then ExternalSeriesMetadata. Weblink parsing is pointless as it updates in externalMetadata
                AniListId = series.ExternalSeriesMetadata.AniListId != 0
                    ? series.ExternalSeriesMetadata.AniListId
                    : ExternalIdParser.GetAniListId(series.Metadata.WebLinks),
                MalId = series.ExternalSeriesMetadata.MalId != 0
                    ? series.ExternalSeriesMetadata.MalId
                    : ExternalIdParser.GetMalId(series.Metadata.WebLinks),
                CbrId = series.ExternalSeriesMetadata.CbrId,
                GoogleBooksId = !string.IsNullOrEmpty(series.ExternalSeriesMetadata.GoogleBooksId)
                    ? series.ExternalSeriesMetadata.GoogleBooksId
                    : ExternalIdParser.GetGoogleBooksId(series.Metadata.WebLinks),
                MangabakaId = (int?) series.MangaBakaId,
                MangaDexId = ExternalIdParser.GetMangaDexId(series.Metadata.WebLinks),
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


    public async Task<PagedList<SeriesDto>> GetRecentlyAddedAsync(int userId, UserParams userParams, SeriesFilterV2Dto seriesFilter,
        CancellationToken ct = default)
    {
        var query = await CreateFilteredSearchQueryableV2(userId, seriesFilter, QueryContext.Dashboard, ct: ct);

        var retSeries = query
            .OrderByDescending(s => s.Created)
            .ProjectToWithProgress<Series, SeriesDto>(mapper, userId)
            .AsSplitQuery()
            .AsNoTracking();

        return await PagedList<SeriesDto>.CreateAsync(retSeries, userParams.PageNumber, userParams.PageSize, ct);
    }

    /// <summary>
    /// Returns Series that the user has some partial progress on. Sorts based on activity. Sort first by User progress, then
    /// by when chapters have been added to series. Restricts progress in the past 30 days and chapters being added to last 7.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="libraryId">Library to restrict to, if 0, will apply to all libraries</param>
    /// <param name="userParams">Pagination information</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<PagedList<SeriesDto>> GetOnDeckAsync(int userId, int libraryId, UserParams userParams, CancellationToken ct = default)
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

    private async Task<IQueryable<Series>> CreateFilteredSearchQueryableV2(int userId, SeriesFilterV2Dto seriesFilter,
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
        query = ApplyLibraryFilter(seriesFilter, query);

        query = ApplyWantToReadFilter(seriesFilter, query, userId);

        query = await ApplyCollectionFilter(seriesFilter, query, userId, userRating, ct);


        query = FilterQueryBuilder.Apply(seriesFilter, query,
            (stmt, q) => BuildFilterGroup(userId, stmt, q));

        query = query
            .WhereIf(allLibraryCount != userLibraries.Count && userLibraries.Count > 0, s => userLibraries.Contains(s.LibraryId))
            .WhereIf(onlyParentSeries, s =>
                s.RelationOf.Count == 0 ||
                s.RelationOf.All(p => p.RelationKind == RelationKind.Prequel))
            .RestrictAgainstAgeRestriction(userRating);


        return query
                .Sort(userId, seriesFilter.SortOptions)
                .AsSplitQuery()
                .ApplyLimit(seriesFilter.LimitTo);
    }

    private async Task<IQueryable<Series>> ApplyCollectionFilter(SeriesFilterV2Dto seriesFilter, IQueryable<Series> query,
        int userId, AgeRestriction userRating, CancellationToken ct = default)
    {
        var collectionStmts = seriesFilter.Statements
            .Where(stmt => stmt.Field == SeriesFilterField.CollectionTags)
            .ToList();
        if (collectionStmts.Count == 0) return query;

        foreach (var collectionStmt in collectionStmts)
        {
            var value = (IList<int>) SeriesFilterFieldValueConverter.ConvertValue(collectionStmt.Field, collectionStmt.Value);

            if (value.Count == 0)
            {
                continue;
            }

            if (collectionStmt.Comparison != FilterComparison.MustContains)
            {
                var collectionSeries = await context.AppUserCollection
                    .Where(uc => uc.Promoted || uc.AppUserId == userId)
                    .Where(uc => value.Contains(uc.Id))
                    .SelectMany(uc => uc.Items)
                    .RestrictAgainstAgeRestriction(userRating)
                    .Select(s => s.Id)
                    .Distinct()
                    .ToListAsync(ct);

                query = query.HasCollectionTags(true, collectionStmt.Comparison, value, collectionSeries);
                continue;
            }

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

            var commonSeries = collectionSeriesLists
                .Aggregate((common, next) => common.Intersect(next)
                    .ToList());

            query = query.Where(s => commonSeries.Contains(s.Id));
        }

        return query;
    }

    private IQueryable<Series> ApplyWantToReadFilter(SeriesFilterV2Dto seriesFilter, IQueryable<Series> query, int userId)
    {
        var wantToReadStmt = seriesFilter.Statements.FirstOrDefault(stmt => stmt.Field == SeriesFilterField.WantToRead);
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

    private static IQueryable<Series> ApplyLibraryFilter(SeriesFilterV2Dto seriesFilter, IQueryable<Series> query)
    {
        var filterIncludeLibs = new List<int>();
        var filterExcludeLibs = new List<int>();

        if (seriesFilter.Statements != null)
        {
            foreach (var stmt in seriesFilter.Statements.Where(stmt => stmt.Field == SeriesFilterField.Libraries))
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
            seriesFilter.Statements = seriesFilter.Statements.Where(stmt => stmt.Field != SeriesFilterField.Libraries).ToList();
        }

        // We now have a list of libraries the user wants it restricted to and libraries the user doesn't want in the list
        // We need to check what the filer combo is to see how to next approach

        if (seriesFilter.Combination == FilterCombination.And)
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

    private static IQueryable<Series> BuildFilterGroup(int userId, SeriesFilterStatementDto statement, IQueryable<Series> query)
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

    public async Task<SeriesMetadataDto?> GetSeriesMetadataAsync(int seriesId, CancellationToken ct = default)
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

    public async Task<IList<MangaFile>> GetFilesForSeriesAsync(int seriesId, CancellationToken ct = default)
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
    private async Task<Tuple<int, int>> GetChunkSize(int libraryId = 0, CancellationToken ct = default)
    {
        var totalSeries = await GetSeriesCount(libraryId, ct);
        return new Tuple<int, int>(totalSeries, 50);
    }

    public async Task<Chunk> GetChunkInfoAsync(int libraryId = 0, CancellationToken ct = default)
    {
        var (totalSeries, chunkSize) = await GetChunkSize(libraryId, ct);

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
    public async Task<IList<GroupedSeriesDto>> GetRecentlyUpdatedSeriesAsync(int userId, UserParams? userParams,
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

    public async Task<IEnumerable<SeriesDto>> GetSeriesForRelationKindAsync(int userId, int seriesId, RelationKind kind,
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

    public async Task<SeriesDto?> GetSeriesForMangaFileAsync(int mangaFileId, int userId, CancellationToken ct = default)
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

    public async Task<SeriesDto?> GetSeriesForChapterAsync(int chapterId, int userId, CancellationToken ct = default)
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
    public async Task<Series?> GetSeriesByFolderPathAsync(string folder, SeriesIncludes includes = SeriesIncludes.None,
        CancellationToken ct = default)
    {
        var normalized = folder.NormalizePath();
        if (string.IsNullOrEmpty(normalized)) return null;

        return await context.Series
            .Where(s => (!string.IsNullOrEmpty(s.FolderPath) && s.FolderPath.Equals(normalized) || (!string.IsNullOrEmpty(s.LowestFolderPath) && s.LowestFolderPath.Equals(normalized))))
            .Includes(includes)
            .SingleOrDefaultAsync(ct);
    }

    public async Task<Series?> GetSeriesThatContainsLowestFolderPathAsync(string path,
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

    public async Task<Series?> GetSeriesByAnyNameAsync(string seriesName, string localizedName, IList<MangaFormat> formats,
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


    public async Task<Series?> GetSeriesByAnyNameAsync(IList<string> names, IList<MangaFormat> formats,
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

    public async Task<IList<Series>> GetAllSeriesByAnyNameAsync(string seriesName, string localizedName, int libraryId,
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
    public async Task<IList<Series>> RemoveSeriesNotInListAsync(IList<ParsedSeries> seenSeries, int libraryId,
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

    public async Task<RelatedSeriesDto> GetRelatedSeriesAsync(int userId, int seriesId, CancellationToken ct = default)
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

    public async Task<PagedList<SeriesDto>> GetWantToReadDtosForUserAsync(int userId, UserParams userParams,
        SeriesFilterV2Dto seriesFilter, CancellationToken ct = default)
    {
        var libraryIds = await context.Library.GetUserLibraries(userId).ToListAsync(ct);
        var seriesIds = await context.AppUser
            .Where(user => user.Id == userId)
            .SelectMany(u => u.WantToRead)
            .Where(s => libraryIds.Contains(s.Series.LibraryId))
            .Select(w => w.Series.Id)
            .Distinct()
            .ToListAsync(ct);

        var query = await CreateFilteredSearchQueryableV2(userId, seriesFilter, QueryContext.None, ct: ct);

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
    public async Task<SeriesDto?> GetSeriesDtoByNamesAndMetadataIdsAsync(IEnumerable<string> names, LibraryType libraryType,
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

    public async Task<Series?> MatchSeriesAsync(ExternalSeriesDetailDto externalSeries, CancellationToken ct = default)
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
    public async Task<int> GetAverageUserRatingAsync(int seriesId, int userId, CancellationToken ct = default)
    {
        var ratings = await context.AppUserRating
            .Where(r => r.SeriesId == seriesId && r.HasBeenRated)
            .ToListAsync(ct);

        if (ratings.Count == 0 || (ratings.Count == 1 && ratings[0].AppUserId == userId))
        {
            return 0;
        }

        var avg = ratings.Average(r => (int?) r.Rating);
        return avg.HasValue ? (int) (avg.Value * 20) : 0;
    }

    public async Task RemoveFromOnDeckAsync(int seriesId, int userId, CancellationToken ct = default)
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

    public async Task ClearOnDeckRemovalAsync(int seriesId, int userId, CancellationToken ct = default)
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

    public async Task<IDictionary<string, IList<SeriesModified>>> GetFolderPathMapAsync(int libraryId,
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
    public async Task<AgeRating> GetMaxAgeRatingFromSeriesAsyncAsync(IEnumerable<int> seriesIds, CancellationToken ct = default)
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

    public Task<List<Series>> GetSeriesForReadStatusTransitionRuleAsync(int userId, ReadStatusTransitionRule rule, bool requireUnReadChapters, CancellationToken ct)
    {
        if (!rule.Enabled || rule.Days <= 0) return Task.FromResult(new List<Series>());

        var cutoffDate = DateTime.UtcNow.AddDays(-rule.Days);
        var excludedStatuses = rule.ExcludedPublicationStatus;

        var seriesProgressStats = context.AppUserProgresses
            .Where(p => p.AppUserId == userId && p.PagesRead > 0)
            .GroupBy(p => p.SeriesId)
            .Select(g => new
            {
                SeriesId = g.Key,
                LastProgressUtc = g.Max(p => p.LastModifiedUtc)
            });

        var seriesChapterCounts = context.Chapter
            .GroupBy(c => c.Volume.SeriesId)
            .Select(g => new
            {
                SeriesId = g.Key,
                TotalChapters = g.Count(),
                LatestChapterAddedUtc = g.Max(c => c.CreatedUtc)
            });

        var seriesReadCounts = context.AppUserProgresses
            .Where(p => p.AppUserId == userId)
            .Join(context.Chapter,
                p => p.ChapterId,
                c => c.Id,
                (p, c) => new { p.SeriesId, IsRead = p.PagesRead >= c.Pages })
            .GroupBy(x => x.SeriesId)
            .Select(g => new
            {
                SeriesId = g.Key,
                ReadChapters = g.Count(x => x.IsRead)
            });

        return context.Series
            .Where(s => !excludedStatuses.Contains(s.Metadata.PublicationStatus))
            .Join(seriesProgressStats,
                s => s.Id,
                sp => sp.SeriesId,
                (s, sp) => new { Series = s, sp.LastProgressUtc })
            .Join(seriesChapterCounts,
                x => x.Series.Id,
                cc => cc.SeriesId,
                (x, cc) => new { x.Series, x.LastProgressUtc, cc.TotalChapters, cc.LatestChapterAddedUtc })
            .Join(seriesReadCounts,
                x => x.Series.Id,
                rc => rc.SeriesId,
                (x, rc) => new
                {
                    x.Series,
                    x.LastProgressUtc,
                    x.TotalChapters,
                    x.LatestChapterAddedUtc,
                    rc.ReadChapters,
                    IsCompletelyRead = x.TotalChapters > 0 && x.TotalChapters == rc.ReadChapters
                })
            .Where(x =>
                x.IsCompletelyRead
                    // Completely read: last progress is >N days before the newest chapter was added
                    ? x.LastProgressUtc < x.LatestChapterAddedUtc.AddDays(-rule.Days)
                    // Not completely read: last progress is >N days ago from today
                    : x.LastProgressUtc < cutoffDate)
            .WhereIf(requireUnReadChapters, x => x.ReadChapters < x.TotalChapters)
            .Select(x => x.Series)
            .Includes(SeriesIncludes.Chapters | SeriesIncludes.ExternalMetadata | SeriesIncludes.Metadata | SeriesIncludes.Library)
            .ToListAsync(ct);
    }
}
