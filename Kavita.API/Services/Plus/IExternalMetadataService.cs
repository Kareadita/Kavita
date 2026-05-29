using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Common;
using Kavita.Models.DTOs.Collection;
using Kavita.Models.DTOs.KavitaPlus.ExternalMetadata;
using Kavita.Models.DTOs.KavitaPlus.ExternalMetadata.Covers;
using Kavita.Models.DTOs.KavitaPlus.Metadata;
using Kavita.Models.DTOs.Metadata.Matching;
using Kavita.Models.DTOs.SeriesDetail;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.Audit;

namespace Kavita.API.Services.Plus;

public interface IExternalMetadataService
{
    public static readonly HashSet<LibraryType> NonEligibleLibraryTypes = [LibraryType.Comic, LibraryType.Book, LibraryType.Image];

    /// <summary>
    /// Retrieves Metadata about a Recommended External Series
    /// </summary>
    /// <param name="aniListId"></param>
    /// <param name="malId"></param>
    /// <param name="seriesId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="KavitaException"></exception>
    Task<ExternalSeriesDetailDto?> GetExternalSeriesDetail(int? aniListId, long? malId, int? seriesId, CancellationToken ct = default);

    /// <summary>
    /// Returns Series Detail data from Kavita+ - Review, Recs, Ratings
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="libraryType"></param>
    /// <param name="trigger"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<SeriesDetailPlusDto?> GetSeriesDetailPlus(int seriesId, LibraryType libraryType,
        MetadataFetchTrigger trigger = MetadataFetchTrigger.OnDemand, CancellationToken ct = default);
    /// <summary>
    /// This is a task that runs on a schedule and slowly fetches data from Kavita+ to keep
    /// data in the DB non-stale and fetched.
    /// </summary>
    /// <remarks>To avoid blasting Kavita+ API, this only processes 25 records. The goal is to slowly build out/refresh the data</remarks>
    /// <returns></returns>
    Task FetchExternalDataTask(CancellationToken ct = default);

    /// <summary>
    /// This is an entry point and provides a level of protection against calling upstream API. Will only allow 100 new
    /// series to fetch data within a day and enqueues background jobs at certain times to fetch that data.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="libraryType"></param>
    /// <param name="ct"></param>
    /// <returns>If the fetch was made</returns>
    Task<bool> FetchSeriesMetadata(int seriesId, LibraryType libraryType,
        MetadataFetchTrigger trigger = MetadataFetchTrigger.SeriesAdded, CancellationToken ct = default);

    Task<IList<MalStackDto>> GetStacksForUser(int userId, CancellationToken ct = default);

    /// <summary>
    /// Returns the match results for a Series from UI Flow
    /// </summary>
    /// <remarks>
    /// Will extract alternative names like Localized name, year will send as ReleaseYear but fallback to Comic Vine syntax if applicable
    /// </remarks>
    /// <param name="dto"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<IList<ExternalSeriesMatchDto>> MatchSeries(MatchSeriesDto dto, CancellationToken ct = default);

    /// <summary>
    /// This will override any sort of matching that was done prior and force it to be what the user Selected
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="ids"></param>
    /// <param name="ct"></param>
    Task FixSeriesMatch(int seriesId, ExternalMetadataIdsDto ids, CancellationToken ct = default);

    /// <summary>
    /// Sets a series to Don't Match and removes all previously cached
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="dontMatch"></param>
    /// <param name="ct"></param>
    Task UpdateSeriesDontMatch(int seriesId, bool dontMatch, CancellationToken ct = default);

    /// <summary>
    /// Given external metadata from Kavita+, write as much as possible to the Kavita series as possible
    /// </summary>
    /// <param name="externalMetadata"></param>
    /// <param name="seriesId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<bool> WriteExternalMetadataToSeries(ExternalSeriesDetailDto externalMetadata, int seriesId, CancellationToken ct = default);

    /// <summary>
    /// Get cover images for a Series/Volume/Chapter
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="volumeId">If set, will get a volume</param>
    /// <param name="chapterId">If set, will filter to chapters (overrides volume)</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<IList<ExternalCoverResponseDto>> GetExternalCovers(int seriesId, int? volumeId = null, int? chapterId = null, CancellationToken ct = default);
}
