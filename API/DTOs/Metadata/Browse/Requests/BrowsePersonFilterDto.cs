using System.Collections.Generic;
using API.DTOs.Filtering;
using API.DTOs.Filtering.v2;
using API.Entities.Enums;

namespace API.DTOs.Metadata.Browse.Requests;
#nullable enable

public sealed record BrowsePersonFilterDto
{
    /// <summary>
    /// Not used - For parity with Series Filter
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// Not used - For parity with Series Filter
    /// </summary>
    public string? Name { get; set; }
    public ICollection<PersonFilterStatementDto> Statements { get; set; } = [];
    public FilterCombination Combination { get; set; } = FilterCombination.And;
    public PersonSortOptions? SortOptions { get; set; }

    /// <summary>
    /// Limit the number of rows returned. Defaults to not applying a limit (aka 0)
    /// </summary>
    public int LimitTo { get; set; } = 0;
}
