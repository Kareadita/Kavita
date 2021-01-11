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
        Task<IEnumerable<SeriesDto>> GetSeriesDtoForLibraryIdAsync(int libraryId);
        Task<IEnumerable<VolumeDto>> GetVolumesDtoAsync(int seriesId);
        IEnumerable<Volume> GetVolumes(int seriesId);
        Task<SeriesDto> GetSeriesDtoByIdAsync(int seriesId);

        Task<Volume> GetVolumeAsync(int volumeId);
        Task<VolumeDto> GetVolumeDtoAsync(int volumeId);

        Task<IEnumerable<Volume>> GetVolumesForSeriesAsync(int[] seriesIds);
    }
}