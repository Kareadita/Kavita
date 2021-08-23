using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs;
using API.DTOs.Filtering;
using API.Entities;
using API.Helpers;

namespace API.Interfaces
{
    public interface ISeriesRepository
    {
        void Add(Series series);
        void Update(Series series);
        Task<Series> GetSeriesByNameAsync(string name);
        Task<bool> DoesSeriesNameExistInLibrary(string name);
        Series GetSeriesByName(string name);

        /// <summary>
        /// Adds user information like progress, ratings, etc
        /// </summary>
        /// <param name="libraryId"></param>
        /// <param name="userId"></param>
        /// <param name="userParams"></param>
        /// <returns></returns>
        Task<PagedList<SeriesDto>> GetSeriesDtoForLibraryIdAsync(int libraryId, int userId, UserParams userParams, FilterDto filter);

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
        /// <summary>
        /// A fast lookup of just the volume information with no tracking.
        /// </summary>
        /// <param name="volumeId"></param>
        /// <returns></returns>
        Task<VolumeDto> GetVolumeDtoAsync(int volumeId);
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

        Task<byte[]> GetVolumeCoverImageAsync(int volumeId);
        Task<byte[]> GetSeriesCoverImageAsync(int seriesId);
        Task<IEnumerable<SeriesDto>> GetInProgress(int userId, int libraryId, UserParams userParams, FilterDto filter);
        Task<PagedList<SeriesDto>> GetRecentlyAdded(int libraryId, int userId, UserParams userParams, FilterDto filter);
        Task<SeriesMetadataDto> GetSeriesMetadata(int seriesId);
        Task<PagedList<SeriesDto>> GetSeriesDtoForCollectionAsync(int collectionId, int userId, UserParams userParams);
        Task<IList<MangaFile>> GetFilesForSeries(int seriesId);
        Task<IEnumerable<SeriesDto>> GetSeriesDtoForIdsAsync(IEnumerable<int> seriesIds, int userId);
    }
}
