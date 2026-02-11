using System.Collections.Generic;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Reader;

namespace Kavita.API.Services;

public interface IAnnotationService
{
    Task<AnnotationDto> CreateAnnotation(int userId, AnnotationDto dto);
    Task<AnnotationDto> UpdateAnnotation(int userId, AnnotationDto dto);
    /// <summary>
    /// Export all annotations for a user, or optionally specify which annotation exactly
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="annotationIds"></param>
    /// <returns></returns>
    Task<string> ExportAnnotations(int userId, IList<int>? annotationIds = null);
}
