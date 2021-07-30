using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.DTOs;
using API.DTOs.Filtering;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using API.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data
{
    public class SeriesRepository : ISeriesRepository
    {
        private readonly DataContext _context;
        private readonly IMapper _mapper;
        private readonly NaturalSortComparer _naturalSortComparer = new ();
        public SeriesRepository(DataContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public void Add(Series series)
        {
            _context.Series.Add(series);
        }

        public void Update(Series series)
        {
            _context.Entry(series).State = EntityState.Modified;
        }

        public async Task<bool> SaveAllAsync()
        {
            return await _context.SaveChangesAsync() > 0;
        }

        public bool SaveAll()
        {
            return _context.SaveChanges() > 0;
        }

        public async Task<Series> GetSeriesByNameAsync(string name)
        {
            return await _context.Series.SingleOrDefaultAsync(x => x.Name == name);
        }

        public async Task<bool> DoesSeriesNameExistInLibrary(string name)
        {
            var libraries = _context.Series
                .AsNoTracking()
                .Where(x => x.Name == name)
                .Select(s => s.LibraryId);

            return await _context.Series
                .AsNoTracking()
                .Where(s => libraries.Contains(s.LibraryId) && s.Name == name)
                .CountAsync() > 1;
        }

        public Series GetSeriesByName(string name)
        {
            return _context.Series.SingleOrDefault(x => x.Name == name);
        }

        public async Task<IEnumerable<Series>> GetSeriesForLibraryIdAsync(int libraryId)
        {
            return await _context.Series
                .Where(s => s.LibraryId == libraryId)
                .OrderBy(s => s.SortName)
                .ToListAsync();
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

        public async Task<IEnumerable<VolumeDto>> GetVolumesDtoAsync(int seriesId, int userId)
        {
            var volumes =  await _context.Volume
                .Where(vol => vol.SeriesId == seriesId)
                .Include(vol => vol.Chapters)
                .OrderBy(volume => volume.Number)
                .ProjectTo<VolumeDto>(_mapper.ConfigurationProvider)
                .AsNoTracking()
                .ToListAsync();

            await AddVolumeModifiers(userId, volumes);
            SortSpecialChapters(volumes);



            return volumes;
        }

        private void SortSpecialChapters(IEnumerable<VolumeDto> volumes)
        {
            foreach (var v in volumes.Where(vDto => vDto.Number == 0))
            {
                v.Chapters = v.Chapters.OrderBy(x => x.Range, _naturalSortComparer).ToList();
            }
        }


        public async Task<IEnumerable<Volume>> GetVolumes(int seriesId)
        {
            return await _context.Volume
                .Where(vol => vol.SeriesId == seriesId)
                .Include(vol => vol.Chapters)
                .ThenInclude(c => c.Files)
                .OrderBy(vol => vol.Number)
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

        public async Task<Volume> GetVolumeAsync(int volumeId)
        {
            return await _context.Volume
                .Include(vol => vol.Chapters)
                .ThenInclude(c => c.Files)
                .SingleOrDefaultAsync(vol => vol.Id == volumeId);
        }

        public async Task<VolumeDto> GetVolumeDtoAsync(int volumeId)
        {
            return await _context.Volume
                .Where(vol => vol.Id == volumeId)
                .AsNoTracking()
                .ProjectTo<VolumeDto>(_mapper.ConfigurationProvider)
                .SingleAsync();

        }

        public async Task<VolumeDto> GetVolumeDtoAsync(int volumeId, int userId)
        {
            var volume = await _context.Volume
                .Where(vol => vol.Id == volumeId)
                .Include(vol => vol.Chapters)
                .ThenInclude(c => c.Files)
                .ProjectTo<VolumeDto>(_mapper.ConfigurationProvider)
                .SingleAsync(vol => vol.Id == volumeId);

            var volumeList = new List<VolumeDto>() {volume};
            await AddVolumeModifiers(userId, volumeList);

            return volumeList[0];
        }

        /// <summary>
        /// Returns all volumes that contain a seriesId in passed array.
        /// </summary>
        /// <param name="seriesIds"></param>
        /// <returns></returns>
        public async Task<IEnumerable<Volume>> GetVolumesForSeriesAsync(int[] seriesIds)
        {
            return await _context.Volume
                .Where(v => seriesIds.Contains(v.SeriesId))
                .ToListAsync();
        }

        public async Task<bool> DeleteSeriesAsync(int seriesId)
        {
            var series = await _context.Series.Where(s => s.Id == seriesId).SingleOrDefaultAsync();
            _context.Series.Remove(series);

            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<Volume> GetVolumeByIdAsync(int volumeId)
        {
            return await _context.Volume.SingleOrDefaultAsync(x => x.Id == volumeId);
        }

        public async Task<Series> GetSeriesByIdAsync(int seriesId)
        {
            return await _context.Series
                .Include(s => s.Volumes)
                .Include(s => s.Metadata)
                .ThenInclude(m => m.CollectionTags)
                .Where(s => s.Id == seriesId)
                .SingleOrDefaultAsync();
        }

        public async Task<int[]> GetChapterIdsForSeriesAsync(int[] seriesIds)
        {
            var series = await _context.Series
                .Where(s => seriesIds.Contains(s.Id))
                .Include(s => s.Volumes)
                .ThenInclude(v => v.Chapters)
                .ToListAsync();

            IList<int> chapterIds = new List<int>();
            foreach (var s in series)
            {
                foreach (var v in s.Volumes)
                {
                    foreach (var c in v.Chapters)
                    {
                        chapterIds.Add(c.Id);
                    }
                }
            }

            return chapterIds.ToArray();
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

        public async Task<byte[]> GetVolumeCoverImageAsync(int volumeId)
        {
            return await _context.Volume
                .Where(v => v.Id == volumeId)
                .Select(v => v.CoverImage)
                .AsNoTracking()
                .SingleOrDefaultAsync();
        }

        public async Task<byte[]> GetSeriesCoverImageAsync(int seriesId)
        {
            return await _context.Series
                .Where(s => s.Id == seriesId)
                .Select(s => s.CoverImage)
                .AsNoTracking()
                .SingleOrDefaultAsync();
        }

        private async Task AddVolumeModifiers(int userId, IReadOnlyCollection<VolumeDto> volumes)
        {
            var userProgress = await _context.AppUserProgresses
                .Where(p => p.AppUserId == userId && volumes.Select(s => s.Id).Contains(p.VolumeId))
                .AsNoTracking()
                .ToListAsync();

            foreach (var v in volumes)
            {
                foreach (var c in v.Chapters)
                {
                    c.PagesRead = userProgress.Where(p => p.ChapterId == c.Id).Sum(p => p.PagesRead);
                }

                v.PagesRead = userProgress.Where(p => p.VolumeId == v.Id).Sum(p => p.PagesRead);
            }
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
                .AsNoTracking();

            return await PagedList<SeriesDto>.CreateAsync(query, userParams.PageNumber, userParams.PageSize);
        }

        /// <summary>
        /// Returns Series that the user has some partial progress on
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="libraryId">Library to restrict to, if 0, will apply to all libraries</param>
        /// <param name="userParams">Pagination information</param>
        /// <param name="filter">Optional (default null) filter on query</param>
        /// <returns></returns>
        public async Task<IEnumerable<SeriesDto>> GetInProgress(int userId, int libraryId, UserParams userParams, FilterDto filter)
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
                    PagesRead = _context.AppUserProgresses.Where(s1 => s1.SeriesId == s.Id).Sum(s1 => s1.PagesRead),
                    progress.AppUserId,
                    LastModified = _context.AppUserProgresses.Where(p => p.Id == progress.Id).Max(p => p.LastModified)
                })
                .AsNoTracking();



            var retSeries = series.Where(s => s.AppUserId == userId
                                              && s.PagesRead > 0
                                              && s.PagesRead < s.Series.Pages
                    /*&& userLibraries.Contains(s.Series.LibraryId)*/
                    /* && formats.Contains(s.Series.Format) */)
                            .OrderByDescending(s => s.LastModified)
                            .Select(s => s.Series)
                            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
                            .AsNoTracking();

            // var retSeries = series
            //     .OrderByDescending(s => s.LastModified)
            //     .Select(s => s.Series)
            //     .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
            //     .AsNoTracking();
            // BUG: Pagination does not work for this query as when we pull the data back, we get multiple rows of the same series
            return await retSeries.ToListAsync();
            //return await PagedList<SeriesDto>.CreateAsync(retSeries, userParams.PageNumber, userParams.PageSize);
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
    }
}
