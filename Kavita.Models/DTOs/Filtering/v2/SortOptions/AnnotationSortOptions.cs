using Kavita.Models.DTOs.Filtering.v2.SortFields;

namespace Kavita.Models.DTOs.Filtering.v2.SortOptions;

/// <summary>
/// All Sorting Options for a query related to Annotation Entity
/// </summary>
public sealed record AnnotationSortOptionDto : ISortOptionDto<AnnotationSortField>
{
    public AnnotationSortField SortField { get; set; }
    public bool IsAscending { get; set; } = true;
}
