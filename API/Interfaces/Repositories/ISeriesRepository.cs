using System.Collections.Generic;
using System.Threading.Tasks;
using API.Data.Scanner;
using API.DTOs;
using API.DTOs.Filtering;
using API.Entities;
using API.Helpers;

namespace API.Interfaces.Repositories
{
    public interface ISeriesRepository
    {
        void Attach(Series series);
        void Update(Series series);
        void Remove(Series series);
        void Remove(IEnumerable<Series> series);
        Task<bool> DoesSeriesNameExistInLibrary(string name);
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
        Task<SeriesDto> GetSeriesDtoByIdAsync(int seriesId, int userId);
        Task<bool> DeleteSeriesAsync(int seriesId);
        Task<Series> GetSeriesByIdAsync(int seriesId);
        Task<IList<Series>> GetSeriesByIdsAsync(IList<int> seriesIds);
        Task<int[]> GetChapterIdsForSeriesAsync(int[] seriesIds);
        Task<IDictionary<int, IList<int>>> GetChapterIdWithSeriesIdForSeriesAsync(int[] seriesIds);
        /// <summary>
        /// Used to add Progress/Rating information to series list.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="series"></param>
        /// <returns></returns>
        Task AddSeriesModifiers(int userId, List<SeriesDto> series);
        Task<string> GetSeriesCoverImageAsync(int seriesId);
        Task<IEnumerable<SeriesDto>> GetInProgress(int userId, int libraryId, UserParams userParams, FilterDto filter);
        Task<PagedList<SeriesDto>> GetRecentlyAdded(int libraryId, int userId, UserParams userParams, FilterDto filter); // NOTE: Probably put this in LibraryRepo
        Task<SeriesMetadataDto> GetSeriesMetadata(int seriesId);
        Task<PagedList<SeriesDto>> GetSeriesDtoForCollectionAsync(int collectionId, int userId, UserParams userParams);
        Task<IList<MangaFile>> GetFilesForSeries(int seriesId);
        Task<IEnumerable<SeriesDto>> GetSeriesDtoForIdsAsync(IEnumerable<int> seriesIds, int userId);
        Task<IList<string>> GetAllCoverImagesAsync();
        Task<IEnumerable<string>> GetLockedCoverImagesAsync();
        Task<PagedList<Series>> GetFullSeriesForLibraryIdAsync(int libraryId, UserParams userParams);
        Task<Series> GetFullSeriesForSeriesIdAsync(int seriesId);
        Task<Chunk> GetChunkInfo(int libraryId = 0);
        Task<IList<SeriesMetadata>> GetSeriesMetadataForIdsAsync(IEnumerable<int> seriesIds);
    }
}
