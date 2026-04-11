using System.Collections.Generic;
using Kavita.Models.DTOs.Filtering.v2.SortOptions;

namespace Kavita.Models.DTOs.Filtering.v2;
#nullable enable

/// <summary>
/// Metadata filtering for v2 API only
/// </summary>
public sealed record FilterV2Dto : IFilterDto<FilterStatementDto>
{
    /// <summary>
    /// Not used in the UI.
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// The name of the filter
    /// </summary>
    public string? Name { get; set; }
    public ICollection<FilterStatementDto> Statements { get; set; } = [];
    public FilterCombination Combination { get; set; } = FilterCombination.And;
    public SeriesSortOptionDto? SortOptions { get; set; }

    /// <summary>
    /// Limit the number of rows returned. Defaults to not applying a limit (aka 0)
    /// </summary>
    public int LimitTo { get; set; } = 0;
}





