using System.Collections.Generic;
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
        
        public async Task<IEnumerable<SeriesDto>> GetSeriesForLibraryIdAsync(int libraryId)
        {
            return await _context.Series
                .Where(series => series.LibraryId == libraryId)
                .OrderBy(s => s.SortName)
                .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider).ToListAsync();
        }

        public async Task<IEnumerable<VolumeDto>> GetVolumesAsync(int seriesId)
        {
            return await _context.Volume
                .Where(vol => vol.SeriesId == seriesId)
                .OrderBy(volume => volume.Number)
                .ProjectTo<VolumeDto>(_mapper.ConfigurationProvider).ToListAsync();
        }
        
        public IEnumerable<VolumeDto> GetVolumesDto(int seriesId)
        {
            return _context.Volume
                .Where(vol => vol.SeriesId == seriesId)
                .OrderBy(vol => vol.Number)
                .ProjectTo<VolumeDto>(_mapper.ConfigurationProvider).ToList();
        }
        
        public IEnumerable<Volume> GetVolumes(int seriesId)
        {
            return _context.Volume
                .Where(vol => vol.SeriesId == seriesId)
                .OrderBy(vol => vol.Number)
                .ToList();
        }

        public async Task<SeriesDto> GetSeriesByIdAsync(int seriesId)
        {
            return await _context.Series.Where(x => x.Id == seriesId)
                .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider).SingleAsync();
        }
    }
}