#nullable enable
using System.Collections.Generic;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.DTOs.Filtering.v2.SortOptions;

namespace Kavita.Models.DTOs.Metadata.Browse.Requests;

public class BrowseAnnotationFilterDto : IFilterDto<AnnotationFilterStatementDto>
{
    /// <summary>
    /// Not used - For parity with Series Filter
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// Not used - For parity with Series Filter
    /// </summary>
    public string? Name { get; set; }
    public ICollection<AnnotationFilterStatementDto> Statements { get; set; } = [];
    public FilterCombination Combination { get; set; } = FilterCombination.And;
    public AnnotationSortOptionDto? SortOptions { get; set; }

    /// <summary>
    /// Limit the number of rows returned. Defaults to not applying a limit (aka 0)
    /// </summary>
    public int LimitTo { get; set; } = 0;
}
