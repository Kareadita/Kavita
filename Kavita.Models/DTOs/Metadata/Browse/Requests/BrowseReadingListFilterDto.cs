using System.Collections.Generic;
using Kavita.Models.DTOs.Filtering;
using Kavita.Models.DTOs.Filtering.v2;

namespace Kavita.Models.DTOs.Metadata.Browse.Requests;
#nullable enable

public record BrowseReadingListFilterDto : IFilterDto<ReadingListFilterStatementDto>
{
    /// <summary>
    /// Not used - For parity with Series Filter
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// Not used - For parity with Series Filter
    /// </summary>
    public string? Name { get; set; }
    public ICollection<ReadingListFilterStatementDto> Statements { get; set; } = [];
    public FilterCombination Combination { get; set; } = FilterCombination.And;
    public ReadingListSortOptions? SortOptions { get; set; }

    /// <summary>
    /// Limit the number of rows returned. Defaults to not applying a limit (aka 0)
    /// </summary>
    public int LimitTo { get; set; } = 0;
}
