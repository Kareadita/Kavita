﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.DTOs;
using API.Entities;
using API.Interfaces.Repositories;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories
{
    public class VolumeRepository : IVolumeRepository
    {
        private readonly DataContext _context;
        private readonly IMapper _mapper;

        public VolumeRepository(DataContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public void Add(Volume volume)
        {
            _context.Volume.Add(volume);
        }

        public void Update(Volume volume)
        {
            _context.Entry(volume).State = EntityState.Modified;
        }

        public void Remove(Volume volume)
        {
            _context.Volume.Remove(volume);
        }

        /// <summary>
        /// Returns a list of non-tracked files for a given volume.
        /// </summary>
        /// <param name="volumeId"></param>
        /// <returns></returns>
        public async Task<IList<MangaFile>> GetFilesForVolume(int volumeId)
        {
            return await _context.Chapter
                .Where(c => volumeId == c.VolumeId)
                .Include(c => c.Files)
                .SelectMany(c => c.Files)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Returns the cover image file for the given volume
        /// </summary>
        /// <param name="volumeId"></param>
        /// <returns></returns>
        public async Task<string> GetVolumeCoverImageAsync(int volumeId)
        {
           return await _context.Volume
                .Where(v => v.Id == volumeId)
                .Select(v => v.CoverImage)
                .AsNoTracking()
                .SingleOrDefaultAsync();
        }

        /// <summary>
        /// Returns all chapter Ids belonging to a list of Volume Ids
        /// </summary>
        /// <param name="volumeIds"></param>
        /// <returns></returns>
        public async Task<IList<int>> GetChapterIdsByVolumeIds(IReadOnlyList<int> volumeIds)
        {
            return await _context.Chapter
                .Where(c => volumeIds.Contains(c.VolumeId))
                .Select(c => c.Id)
                .ToListAsync();
        }

        /// <summary>
        /// Returns all volumes that contain a seriesId in passed array.
        /// </summary>
        /// <param name="seriesIds"></param>
        /// <returns></returns>
        public async Task<IEnumerable<Volume>> GetVolumesForSeriesAsync(IList<int> seriesIds, bool includeChapters = false)
        {
            var query = _context.Volume
                .Where(v => seriesIds.Contains(v.SeriesId));

            if (includeChapters)
            {
                query = query.Include(v => v.Chapters);
            }
            return await query.ToListAsync();
        }

        /// <summary>
        /// Returns an individual Volume including Chapters and Files and Reading Progress for a given volumeId
        /// </summary>
        /// <param name="volumeId"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
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
        /// Returns the full Volumes including Chapters and Files for a given series
        /// </summary>
        /// <param name="seriesId"></param>
        /// <returns></returns>
        public async Task<IEnumerable<Volume>> GetVolumes(int seriesId)
        {
            return await _context.Volume
                .Where(vol => vol.SeriesId == seriesId)
                .Include(vol => vol.Chapters)
                .ThenInclude(c => c.Files)
                .OrderBy(vol => vol.Number)
                .ToListAsync();
        }

        /// <summary>
        /// Returns a single volume with Chapter and Files
        /// </summary>
        /// <param name="volumeId"></param>
        /// <returns></returns>
        public async Task<Volume> GetVolumeAsync(int volumeId)
        {
            return await _context.Volume
                .Include(vol => vol.Chapters)
                .ThenInclude(c => c.Files)
                .SingleOrDefaultAsync(vol => vol.Id == volumeId);
        }


        /// <summary>
        /// Returns all volumes for a given series with progress information attached. Includes all Chapters as well.
        /// </summary>
        /// <param name="seriesId"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
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

        public async Task<Volume> GetVolumeByIdAsync(int volumeId)
        {
            return await _context.Volume.SingleOrDefaultAsync(x => x.Id == volumeId);
        }


        private static void SortSpecialChapters(IEnumerable<VolumeDto> volumes)
        {
            var sorter = new NaturalSortComparer();
            foreach (var v in volumes.Where(vDto => vDto.Number == 0))
            {
                v.Chapters = v.Chapters.OrderBy(x => x.Range, sorter).ToList();
            }
        }


        private async Task AddVolumeModifiers(int userId, IReadOnlyCollection<VolumeDto> volumes)
        {
            var volIds = volumes.Select(s => s.Id);
            var userProgress = await _context.AppUserProgresses
                .Where(p => p.AppUserId == userId && volIds.Contains(p.VolumeId))
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


    }
}
