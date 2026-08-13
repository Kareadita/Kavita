using Kavita.Models.DTOs.Filtering.v2.SortFields;
using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs.Filtering.v2.SortOptions;

/// <summary>
/// All Sorting Options for a query related to Reading List Entity
/// </summary>
public sealed record ReadingListSortOptionDto : ISortOptionDto<ReadingListSortField>
{
    [EnumDataType(typeof(ReadingListSortField))]
    public ReadingListSortField SortField { get; set; }
    public bool IsAscending { get; set; } = true;
}