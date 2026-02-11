using System.Collections.Generic;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Collection;
using Kavita.Models.DTOs.KavitaPlus.Metadata;
using Kavita.Models.DTOs.Metadata.Matching;
using Kavita.Models.DTOs.SeriesDetail;
using Kavita.Models.Entities.Enums;

namespace Kavita.API.Services.Plus;

public interface IExternalMetadataService
{
    public static readonly HashSet<LibraryType> NonEligibleLibraryTypes = [LibraryType.Comic, LibraryType.Book, LibraryType.Image];

    Task<ExternalSeriesDetailDto?> GetExternalSeriesDetail(int? aniListId, long? malId, int? seriesId);
    Task<SeriesDetailPlusDto?> GetSeriesDetailPlus(int seriesId, LibraryType libraryType);
    Task FetchExternalDataTask();
    /// <summary>
    /// This is an entry point and provides a level of protection against calling upstream API. Will only allow 100 new
    /// series to fetch data within a day and enqueues background jobs at certain times to fetch that data.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="libraryType"></param>
    /// <returns>If the fetch was made</returns>
    Task<bool> FetchSeriesMetadata(int seriesId, LibraryType libraryType);

    Task<IList<MalStackDto>> GetStacksForUser(int userId);
    Task<IList<ExternalSeriesMatchDto>> MatchSeries(MatchSeriesDto dto);
    Task FixSeriesMatch(int seriesId, int? aniListId, long? malId, int? cbrId);
    Task UpdateSeriesDontMatch(int seriesId, bool dontMatch);
    Task<bool> WriteExternalMetadataToSeries(ExternalSeriesDetailDto externalMetadata, int seriesId);
}
