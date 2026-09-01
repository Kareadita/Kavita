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
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.Audit;

namespace Kavita.API.Services.Plus;

public interface IExternalMetadataService
{
    /// <summary>
    /// Retrieves Metadata about a Recommended External Series
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="request"></param>
    /// /// <param name="recommendedSeriesId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="KavitaException"></exception>
    Task<ExternalSeriesDetailDto?> GetExternalSeriesDetail(int seriesId, MetadataRequest request, int? recommendedSeriesId, CancellationToken ct = default);

    /// <summary>
    /// This is a task that runs on a schedule and slowly fetches data from Kavita+ to keep
    /// data in the DB non-stale and fetched.
    /// </summary>
    /// <remarks>To avoid blasting Kavita+ API, this only processes 25 records. The goal is to slowly build out/refresh the data</remarks>
    /// <returns></returns>
    Task FetchExternalDataTask(CancellationToken ct = default);

    Task<IList<MalStackDto>> GetStacksForUser(int userId, CancellationToken ct = default);

    /// <summary>
    /// Returns the match results for a Series from UI Flow
    /// </summary>
    /// <remarks>
    /// Will extract alternative names like Localized name, year will send as ReleaseYear but fallback to Comic Vine syntax if applicable
    /// </remarks>
    /// <param name="dto"></param>
    /// <param name="ct"></param>
    /// <returns>The matches and the provider they came from, or null if the series doesn't exist</returns>
    Task<MatchSeriesResultDto?> MatchSeries(MatchSeriesDto dto, CancellationToken ct = default);

    /// <summary>
    /// This will override any sort of matching that was done prior and force it to be what the user Selected
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="ids"></param>
    /// <param name="provider">The provider the match came from.</param>
    /// <param name="ct"></param>
    Task FixSeriesMatch(int seriesId, ExternalMetadataIdsDto ids, MetadataProvider? provider = null, CancellationToken ct = default);

    /// <summary>
    /// Sets a series to Don't Match and removes all previously cached
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="dontMatch"></param>
    /// <param name="ct"></param>
    Task UpdateSeriesDontMatch(int seriesId, bool dontMatch, CancellationToken ct = default);

    /// <summary>
    /// Changes (or clears) which <see cref="MetadataProvider"/> a Series should match against, overriding its Library's default
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="metadataProviderOverride">Null clears the override</param>
    /// <param name="ct"></param>
    Task UpdateSeriesMetadataProviderOverride(int seriesId, MetadataProvider? metadataProviderOverride, CancellationToken ct = default);

    /// <summary>
    /// Given external metadata from Kavita+, write as much as possible to the Kavita series as possible
    /// </summary>
    /// <param name="externalMetadata"></param>
    /// <param name="seriesId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<bool> WriteExternalMetadataToSeries(ExternalSeriesDetailDto externalMetadata, int seriesId,
        MetadataFetchTrigger trigger = MetadataFetchTrigger.OnDemand, CancellationToken ct = default);

    /// <summary>
    /// Get cover images for a Series/Volume/Chapter
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="volumeId">If set, will get a volume</param>
    /// <param name="chapterId">If set, will filter to chapters (overrides volume)</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<IList<ExternalCoverResponseDto>> GetExternalCovers(int seriesId, int? volumeId = null,
        int? chapterId = null, CancellationToken ct = default);

    /// <summary>
    /// Loads external series metadata. If ids are presents, loads directly otherwise goes through the match flow
    /// And picks the best match (Requires just one result > 0.9)
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="libraryType"></param>
    /// <param name="trigger"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<SeriesDetailPlusDto?> TryMatchAndLoadMetadataForSeries(int seriesId, LibraryType libraryType, MetadataFetchTrigger trigger,
        CancellationToken ct = default);

    /// <summary>
    /// Determines whether changing the Series' Name to <paramref name="proposedName"/> would orphan merged files on
    /// disk, i.e. the current Name still anchors a folder on disk that is not covered by the Series' LocalizedName or
    /// OriginalName. When true, the scanner would split those files into a new series and the write should be rejected.
    /// </summary>
    /// <param name="series">The Series being edited. Loaded scalar fields (Name, NormalizedLocalizedName, NormalizedOriginalName) are used.</param>
    /// <param name="proposedName">The new (raw, un-normalized) name.</param>
    /// <param name="ct"></param>
    Task<bool> WouldNameChangeOrphanMergedFiles(Series series, string? proposedName, CancellationToken ct = default);

    /// <summary>
    /// Determines whether changing the Series' LocalizedName to <paramref name="proposedLocalizedName"/> would orphan
    /// merged files on disk, i.e. the current LocalizedName still anchors a folder on disk that is not covered by the
    /// Series' Name or OriginalName. When true, the scanner would split those files into a new series and the write
    /// should be rejected.
    /// </summary>
    /// <param name="series">The Series being edited. Loaded scalar fields (LocalizedName, NormalizedName, NormalizedOriginalName) are used.</param>
    /// <param name="proposedLocalizedName">The new (raw, un-normalized) localized name. Null or empty represents clearing the field.</param>
    /// <param name="ct"></param>
    Task<bool> WouldLocalizedNameChangeOrphanMergedFiles(Series series, string? proposedLocalizedName, CancellationToken ct = default);
}
