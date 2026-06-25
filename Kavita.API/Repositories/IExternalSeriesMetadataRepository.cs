using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs.KavitaPlus.Manage;
using Kavita.Models.DTOs.SeriesDetail;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Metadata;

namespace Kavita.API.Repositories;

public interface IExternalSeriesMetadataRepository
{
    void Attach(ExternalSeriesMetadata metadata);
    void Attach(ExternalRating rating);
    void Attach(ExternalReview review);
    void Remove(IEnumerable<ExternalReview>? reviews);
    void Remove(IEnumerable<ExternalRating>? ratings);
    void Remove(IEnumerable<ExternalRecommendation>? recommendations);
    void Remove(ExternalSeriesMetadata metadata);
    Task<ExternalSeriesMetadata?> GetExternalSeriesMetadata(int seriesId, CancellationToken ct = default);
    Task<bool> NeedsDataRefresh(int seriesId, CancellationToken ct = default);
    Task<SeriesDetailPlusDto?> GetSeriesDetailPlusDto(int seriesId, CancellationToken ct = default);
    Task LinkRecommendationsToSeries(Series series, CancellationToken ct = default);
    Task<List<int>> GetSeriesThatNeedExternalMetadata(int limit, bool includeStaleData = false, CancellationToken ct = default);
    Task<PagedList<ManageMatchSeriesDto>> GetAllSeries(ManageMatchFilterDto filter, UserParams userParams, CancellationToken ct = default);
}
