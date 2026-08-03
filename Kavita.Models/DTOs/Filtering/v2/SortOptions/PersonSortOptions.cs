using Kavita.Models.DTOs.Filtering.v2.SortFields;
using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs.Filtering.v2.SortOptions;

/// <summary>
/// All Sorting Options for a query related to Person Entity
/// </summary>
public sealed record PersonSortOptionDto : ISortOptionDto<PersonSortField>
{
    [EnumDataType(typeof(PersonSortField))]
    public PersonSortField SortField { get; set; }
    public bool IsAscending { get; set; } = true;
}