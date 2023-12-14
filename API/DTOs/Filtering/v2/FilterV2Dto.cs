using System.Collections.Generic;

namespace API.DTOs.Filtering.v2;
#nullable enable

/// <summary>
/// Metadata filtering for v2 API only
/// </summary>
public class FilterV2Dto
{
    /// <summary>
    /// Not used in the UI.
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// The name of the filter
    /// </summary>
    public string? Name { get; set; }
    public ICollection<FilterStatementDto> Statements { get; set; } = new List<FilterStatementDto>();
    public FilterCombination Combination { get; set; } = FilterCombination.And;
    public SortOptions? SortOptions { get; set; }

    /// <summary>
    /// Limit the number of rows returned. Defaults to not applying a limit (aka 0)
    /// </summary>
    public int LimitTo { get; set; } = 0;
}





