using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
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
        
        public async Task<IEnumerable<SeriesDto>> GetSeriesDtoForLibraryIdAsync(int libraryId, int userId)
        {
            // if (userId > 0)
            // {
            //     return await _context.AppUserProgresses
            //         .Include(p => p.Series)
            //         .Where(p => p.AppUserId == userId && p.Series.LibraryId == libraryId)
            //         .Select(p => p.Series)
            //         .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
            //         //.Select(s => s.PagesRead = )
            //         .ToListAsync();
            // }
            
            var sw = Stopwatch.StartNew();
            var series = await _context.Series
                .Where(s => s.LibraryId == libraryId)
                .OrderBy(s => s.SortName)
                .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
            
            if (userId > 0)
            {
                var userProgress = await _context.AppUserProgresses
                    .Where(p => p.AppUserId == userId && series.Select(s => s.Id).Contains(p.SeriesId))
                    .ToListAsync();

                foreach (var s in series)
                {
                    s.PagesRead = userProgress.Where(p => p.SeriesId == s.Id).Sum(p => p.PagesRead);
                }
            }

            Console.WriteLine("Processed GetSeriesDtoForLibraryIdAsync in {0} milliseconds", sw.ElapsedMilliseconds);
            return series;
        }

        public async Task<IEnumerable<VolumeDto>> GetVolumesDtoAsync(int seriesId, int userId)
        {
            var volumes =  await _context.Volume
                .Where(vol => vol.SeriesId == seriesId)
                .OrderBy(volume => volume.Number)
                .ProjectTo<VolumeDto>(_mapper.ConfigurationProvider)
                .AsNoTracking()
                .ToListAsync();
            var userProgress = await _context.AppUserProgresses
                .Where(p => p.AppUserId == userId && volumes.Select(s => s.Id).Contains(p.VolumeId))
                .AsNoTracking()
                .ToListAsync();

            foreach (var v in volumes)
            {
                v.PagesRead = userProgress.Where(p => p.VolumeId == v.Id).Sum(p => p.PagesRead);
            }

            return volumes;

        }

        public IEnumerable<Volume> GetVolumes(int seriesId)
        {
            return _context.Volume
                .Where(vol => vol.SeriesId == seriesId)
                .Include(vol => vol.Files)
                .OrderBy(vol => vol.Number)
                .ToList();
        }

        public async Task<SeriesDto> GetSeriesDtoByIdAsync(int seriesId)
        {
            return await _context.Series.Where(x => x.Id == seriesId)
                .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider).SingleAsync();
        }

        public async Task<Volume> GetVolumeAsync(int volumeId)
        {
            return await _context.Volume
                .Include(vol => vol.Files)
                .SingleOrDefaultAsync(vol => vol.Id == volumeId);
        }

        public async Task<VolumeDto> GetVolumeDtoAsync(int volumeId)
        {
            return await _context.Volume
                .Where(vol => vol.Id == volumeId)
                .Include(vol => vol.Files)
                .ProjectTo<VolumeDto>(_mapper.ConfigurationProvider)
                .SingleAsync(vol => vol.Id == volumeId);
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
    }
}