using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.DTOs.SeriesDetail;

namespace Kavita.API.Services;

public interface ISeriesService
{
    Task<SeriesDetailDto> GetSeriesDetail(int seriesId, int userId, CancellationToken ct = default);
    Task<bool> UpdateSeriesMetadata(UpdateSeriesMetadataDto updateSeriesMetadataDto, CancellationToken ct = default);
    Task<bool> DeleteMultipleSeries(IList<int> seriesIds, CancellationToken ct = default);
    Task<bool> UpdateRelatedSeries(UpdateRelatedSeriesDto dto, CancellationToken ct = default);
    Task<RelatedSeriesDto> GetRelatedSeries(int userId, int seriesId, CancellationToken ct = default);
    Task<NextExpectedChapterDto> GetEstimatedChapterCreationDate(int seriesId, int userId, CancellationToken ct = default);
    Task<PagedList<SeriesDto>> GetCurrentlyReading(int userId, int requestingUserId, UserParams userParams, CancellationToken ct = default);
    Task<List<SeriesFilterStatementDto>> GetProfilePrivacyStatements(int userId, int requestingUserId, CancellationToken ct = default);
}
