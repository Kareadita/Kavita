using System.Collections.Generic;
using System.Threading.Tasks;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.DTOs.SeriesDetail;

namespace Kavita.API.Services;

public interface ISeriesService
{
    Task<SeriesDetailDto> GetSeriesDetail(int seriesId, int userId);
    Task<bool> UpdateSeriesMetadata(UpdateSeriesMetadataDto updateSeriesMetadataDto);
    Task<bool> DeleteMultipleSeries(IList<int> seriesIds);
    Task<bool> UpdateRelatedSeries(UpdateRelatedSeriesDto dto);
    Task<RelatedSeriesDto> GetRelatedSeries(int userId, int seriesId);
    Task<NextExpectedChapterDto> GetEstimatedChapterCreationDate(int seriesId, int userId);
    Task<PagedList<SeriesDto>> GetCurrentlyReading(int userId, int requestingUserId, UserParams userParams);
    Task<List<FilterStatementDto>> GetProfilePrivacyStatements(int userId, int requestingUserId);
}
