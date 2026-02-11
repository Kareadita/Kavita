using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Annotations;
using Kavita.Models.DTOs.Metadata.Browse.Requests;
using Kavita.Models.DTOs.Reader;
using Kavita.Models.Entities.User;

namespace Kavita.API.Repositories;

public interface IAnnotationRepository
{
    void Attach(AppUserAnnotation annotation);
    void Update(AppUserAnnotation annotation);
    void Remove(AppUserAnnotation annotation);
    void Remove(IEnumerable<AppUserAnnotation> annotations);
    Task<AnnotationDto?> GetAnnotationDto(int id, CancellationToken ct = default);
    Task<AppUserAnnotation?> GetAnnotation(int id, CancellationToken ct = default);
    Task<IList<AppUserAnnotation>> GetAllAnnotations(CancellationToken ct = default);
    Task<IList<AppUserAnnotation>> GetAnnotations(int userId, IList<int> ids, CancellationToken ct = default);
    Task<IList<FullAnnotationDto>> GetFullAnnotationsByUserIdAsync(int userId, CancellationToken ct = default);
    Task<IList<FullAnnotationDto>> GetFullAnnotations(int userId, IList<int> annotationIds, CancellationToken ct = default);
    Task<PagedList<AnnotationDto>> GetAnnotationDtos(int userId, BrowseAnnotationFilterDto filter, UserParams userParams, CancellationToken ct = default);
    Task<List<SeriesDto>> GetSeriesWithAnnotations(int userId, CancellationToken ct = default);
}
