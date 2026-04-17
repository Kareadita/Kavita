using Kavita.Models.DTOs.Filtering.v2.SortFields;

namespace Kavita.Models.DTOs.Filtering.v2.SortOptions;

/// <summary>
/// Sorting Options for a query
/// </summary>
public sealed record SeriesSortOptionDto : ISortOptionDto<SeriesSortField>
{
    public SeriesSortField SortField { get; set; }
    public bool IsAscending { get; set; } = true;
}






