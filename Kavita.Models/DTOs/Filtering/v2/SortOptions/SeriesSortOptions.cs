using Kavita.Models.DTOs.Filtering.v2.SortFields;

namespace Kavita.Models.DTOs.Filtering.v2.SortOptions;

/// <summary>
/// Sorting Options for a query
/// </summary>
public sealed record SeriesSortOptionDto
{
    public SeriesSortField SeriesSortField { get; set; }
    public bool IsAscending { get; set; } = true;
}






