using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data.Scanner;
using API.DTOs;
using API.DTOs.CollectionTags;
using API.DTOs.Filtering;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using API.Interfaces.Repositories;
using API.Services.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories
{
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
        public async Task<bool> DoesSeriesNameExistInLibrary(string name, MangaFormat format)
        {
            var libraries = _context.Series
                .AsNoTracking()
                .Where(x => x.Name.Equals(name) && x.Format == format)
                .Select(s => s.LibraryId);

            return await _context.Series
                .AsNoTracking()
                .Where(s => libraries.Contains(s.LibraryId) && s.Name.Equals(name) && s.Format == format)
                .CountAsync() > 1;
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
                .Include(s => s.Library)
                .Include(s => s.Volumes)
                .ThenInclude(v => v.Chapters)
                .ThenInclude(c => c.Files)
                .AsSplitQuery()
                .SingleOrDefaultAsync();
        }

        public async Task<PagedList<SeriesDto>> GetSeriesDtoForLibraryIdAsync(int libraryId, int userId, UserParams userParams, FilterDto filter)
        {
            var formats = filter.GetSqlFilter();
            var query =  _context.Series
                .Where(s => s.LibraryId == libraryId && formats.Contains(s.Format))
                .OrderBy(s => s.SortName)
                .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
                .AsNoTracking();

            return await PagedList<SeriesDto>.CreateAsync(query, userParams.PageNumber, userParams.PageSize);
        }

        public async Task<IEnumerable<SearchResultDto>> SearchSeries(int[] libraryIds, string searchQuery)
        {
            return await _context.Series
                .Where(s => libraryIds.Contains(s.LibraryId))
                .Where(s => EF.Functions.Like(s.Name, $"%{searchQuery}%")
                            || EF.Functions.Like(s.OriginalName, $"%{searchQuery}%")
                            || EF.Functions.Like(s.LocalizedName, $"%{searchQuery}%"))
                .Include(s => s.Library)
                .OrderBy(s => s.SortName)
                .AsNoTracking()
                .ProjectTo<SearchResultDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
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
            _context.Series.Remove(series);

            return await _context.SaveChangesAsync() > 0;
        }


        /// <summary>
        /// Returns Volumes, Metadata, and Collection Tags
        /// </summary>
        /// <param name="seriesId"></param>
        /// <returns></returns>
        public async Task<Series> GetSeriesByIdAsync(int seriesId)
        {
            return await _context.Series
                .Include(s => s.Volumes)
                .Include(s => s.Metadata)
                .ThenInclude(m => m.CollectionTags)
                .Where(s => s.Id == seriesId)
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
                .ToListAsync();
        }

        public async Task<int[]> GetChapterIdsForSeriesAsync(int[] seriesIds)
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
        /// This returns a dictonary mapping seriesId -> list of chapters back for each series id passed
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
                if (rating == null) continue;
                s.UserRating = rating.Rating;
                s.UserReview = rating.Review;
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
            var formats = filter.GetSqlFilter();

            if (libraryId == 0)
            {
                var userLibraries = _context.Library
                    .Include(l => l.AppUsers)
                    .Where(library => library.AppUsers.Any(user => user.Id == userId))
                    .AsNoTracking()
                    .Select(library => library.Id)
                    .ToList();

                var allQuery = _context.Series
                    .Where(s => userLibraries.Contains(s.LibraryId) && formats.Contains(s.Format))
                    .OrderByDescending(s => s.Created)
                    .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
                    .AsNoTracking();

                return await PagedList<SeriesDto>.CreateAsync(allQuery, userParams.PageNumber, userParams.PageSize);
            }

            var query = _context.Series
                .Where(s => s.LibraryId == libraryId && formats.Contains(s.Format))
                .OrderByDescending(s => s.Created)
                .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
                .AsSplitQuery()
                .AsNoTracking();

            return await PagedList<SeriesDto>.CreateAsync(query, userParams.PageNumber, userParams.PageSize);
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
        public async Task<IEnumerable<SeriesDto>> GetOnDeck(int userId, int libraryId, UserParams userParams, FilterDto filter)
        {
            var formats = filter.GetSqlFilter();
            IList<int> userLibraries;
            if (libraryId == 0)
            {
                userLibraries = _context.Library
                    .Include(l => l.AppUsers)
                    .Where(library => library.AppUsers.Any(user => user.Id == userId))
                    .AsNoTracking()
                    .Select(library => library.Id)
                    .ToList();
            }
            else
            {
                userLibraries = new List<int>() {libraryId};
            }

            var series = _context.Series
                .Where(s => formats.Contains(s.Format) && userLibraries.Contains(s.LibraryId))
                .Join(_context.AppUserProgresses, s => s.Id, progress => progress.SeriesId, (s, progress) => new
                {
                    Series = s,
                    PagesRead = _context.AppUserProgresses.Where(s1 => s1.SeriesId == s.Id && s1.AppUserId == userId).Sum(s1 => s1.PagesRead),
                    progress.AppUserId,
                    LastModified = _context.AppUserProgresses.Where(p => p.Id == progress.Id && p.AppUserId == userId).Max(p => p.LastModified)
                })
                .AsNoTracking();

            var retSeries = series.Where(s => s.AppUserId == userId
                                              && s.PagesRead > 0
                                              && s.PagesRead < s.Series.Pages)
                            .OrderByDescending(s => s.LastModified)
                            .ThenByDescending(s => s.Series.LastModified)
                            .Select(s => s.Series)
                            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
                            .AsSplitQuery()
                            .AsNoTracking();

            // Pagination does not work for this query as when we pull the data back, we get multiple rows of the same series. See controller for pagination code
            return await retSeries.ToListAsync();
        }

        public async Task<SeriesMetadataDto> GetSeriesMetadata(int seriesId)
        {
            var metadataDto = await _context.SeriesMetadata
                .Where(metadata => metadata.SeriesId == seriesId)
                .AsNoTracking()
                .ProjectTo<SeriesMetadataDto>(_mapper.ConfigurationProvider)
                .SingleOrDefaultAsync();

            if (metadataDto != null)
            {
                metadataDto.Tags = await _context.CollectionTag
                    .Include(t => t.SeriesMetadatas)
                    .Where(t => t.SeriesMetadatas.Select(s => s.SeriesId).Contains(seriesId))
                    .ProjectTo<CollectionTagDto>(_mapper.ConfigurationProvider)
                    .AsNoTracking()
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
            // TODO: Think about making this bigger depending on number of files a user has in said library
            // and number of cores and amount of memory. We can then make an optimal choice
            var totalSeries = await GetSeriesCount(libraryId);
            // var procCount = Math.Max(Environment.ProcessorCount - 1, 1);
            //
            // if (totalSeries < procCount * 2 || totalSeries < 50)
            // {
            //     return new Tuple<int, int>(totalSeries, totalSeries);
            // }
            //
            // return new Tuple<int, int>(totalSeries, Math.Max(totalSeries / procCount, 50));
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
    }
}
