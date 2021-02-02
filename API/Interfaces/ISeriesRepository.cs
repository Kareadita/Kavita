using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;

namespace API.Interfaces
{
    public interface ISeriesRepository
    {
        void Add(Series series);
        void Update(Series series);
        Task<Series> GetSeriesByNameAsync(string name);
        Series GetSeriesByName(string name);
        Task<IEnumerable<SeriesDto>> GetSeriesDtoForLibraryIdAsync(int libraryId, int userId);
        Task<IEnumerable<Series>> GetSeriesForLibraryIdAsync(int libraryId);
        Task<IEnumerable<VolumeDto>> GetVolumesDtoAsync(int seriesId, int userId);
        IEnumerable<Volume> GetVolumes(int seriesId);
        Task<SeriesDto> GetSeriesDtoByIdAsync(int seriesId, int userId);

        Task<Volume> GetVolumeAsync(int volumeId);
        Task<VolumeDto> GetVolumeDtoAsync(int volumeId, int userId);

        Task<IEnumerable<Volume>> GetVolumesForSeriesAsync(int[] seriesIds);
        Task<bool> DeleteSeriesAsync(int seriesId);
        Task<Volume> GetVolumeByIdAsync(int volumeId);
        Task<Series> GetSeriesByIdAsync(int seriesId);

        //Task<MangaFileDto> GetVolumeMangaFileDtos(int volumeId);
        Task<int[]> GetChapterIdsForSeriesAsync(int[] seriesIds);
    }
}