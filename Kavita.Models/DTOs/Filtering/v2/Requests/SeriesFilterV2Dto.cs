using System.Collections.Generic;
using Kavita.Models.DTOs.Filtering.v2.SortOptions;

namespace Kavita.Models.DTOs.Filtering.v2.Requests;
#nullable enable

/// <summary>
/// Metadata filtering for v2 API only
/// </summary>
public sealed record SeriesFilterV2Dto : IFilterDto<SeriesFilterStatementDto, SeriesSortOptionDto>
{
    /// <summary>
    /// Not used in the UI.
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// The name of the filter
    /// </summary>
    public string? Name { get; set; }
    public ICollection<SeriesFilterStatementDto> Statements { get; set; } = [];
    public FilterCombination Combination { get; set; } = FilterCombination.And;
    public SeriesSortOptionDto? SortOptions { get; set; }
    public FilterEntityType EntityType => FilterEntityType.Series;

    /// <summary>
    /// Limit the number of rows returned. Defaults to not applying a limit (aka 0)
    /// </summary>
    public int LimitTo { get; set; } = 0;
}





