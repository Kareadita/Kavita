using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;

namespace API.Interfaces
{
    public interface ISeriesRepository
    {
        void Update(Series series);
        Task<bool> SaveAllAsync();
        Task<Series> GetSeriesByNameAsync(string name);
        Series GetSeriesByName(string name);
        bool SaveAll();
        Task<IEnumerable<SeriesDto>> GetSeriesForLibraryIdAsync(int libraryId);
        Task<IEnumerable<VolumeDto>> GetVolumesAsync(int seriesId);
        IEnumerable<VolumeDto> GetVolumesDto(int seriesId);
        IEnumerable<Volume> GetVolumes(int seriesId);
        Task<SeriesDto> GetSeriesByIdAsync(int seriesId);
        
    }
}