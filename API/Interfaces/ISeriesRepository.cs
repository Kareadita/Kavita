using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
using API.Helpers;

namespace API.Interfaces
{
    public interface ISeriesRepository
    {
        void Add(Series series);
        void Update(Series series);
        Task<Series> GetSeriesByNameAsync(string name);
        Series GetSeriesByName(string name);

        /// <summary>
        /// Adds user information like progress, ratings, etc
        /// </summary>
        /// <param name="libraryId"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        Task<PagedList<SeriesDto>> GetSeriesDtoForLibraryIdAsync(int libraryId, int userId, UserParams userParams);

        /// <summary>
        /// Does not add user information like progress, ratings, etc.
        /// </summary>
        /// <param name="libraryIds"></param>
        /// <param name="searchQuery">Series name to search for</param>
        /// <returns></returns>
        Task<IEnumerable<SearchResultDto>> SearchSeries(int[] libraryIds, string searchQuery);
        Task<IEnumerable<Series>> GetSeriesForLibraryIdAsync(int libraryId);
        Task<IEnumerable<VolumeDto>> GetVolumesDtoAsync(int seriesId, int userId);
        Task<IEnumerable<Volume>> GetVolumes(int seriesId);
        Task<SeriesDto> GetSeriesDtoByIdAsync(int seriesId, int userId);
        Task<Volume> GetVolumeAsync(int volumeId);
        Task<VolumeDto> GetVolumeDtoAsync(int volumeId, int userId);
        Task<IEnumerable<Volume>> GetVolumesForSeriesAsync(int[] seriesIds);
        Task<bool> DeleteSeriesAsync(int seriesId);
        Task<Volume> GetVolumeByIdAsync(int volumeId);
        Task<Series> GetSeriesByIdAsync(int seriesId);
        Task<int[]> GetChapterIdsForSeriesAsync(int[] seriesIds);
        /// <summary>
        /// Used to add Progress/Rating information to series list.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="series"></param>
        /// <returns></returns>
        Task AddSeriesModifiers(int userId, List<SeriesDto> series);
    }
}