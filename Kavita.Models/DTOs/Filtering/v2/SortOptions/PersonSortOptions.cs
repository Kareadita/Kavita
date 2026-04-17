using Kavita.Models.DTOs.Filtering.v2.SortFields;

namespace Kavita.Models.DTOs.Filtering.v2.SortOptions;

/// <summary>
/// All Sorting Options for a query related to Person Entity
/// </summary>
public sealed record PersonSortOptionDto : ISortOptionDto<PersonSortField>
{
    public PersonSortField SortField { get; set; }
    public bool IsAscending { get; set; } = true;
}
