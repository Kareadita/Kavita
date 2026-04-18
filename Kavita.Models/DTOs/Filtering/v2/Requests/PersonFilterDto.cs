using System.Collections.Generic;
using Kavita.Models.DTOs.Filtering.v2.SortOptions;
using NotImplementedException = System.NotImplementedException;

namespace Kavita.Models.DTOs.Filtering.v2.Requests;
#nullable enable

public sealed record PersonFilterDto : IFilterDto<PersonFilterStatementDto, PersonSortOptionDto>
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
    public PersonSortOptionDto? SortOptions { get; set; }
    public FilterEntityType EntityType => FilterEntityType.Person;

    /// <summary>
    /// Limit the number of rows returned. Defaults to not applying a limit (aka 0)
    /// </summary>
    public int LimitTo { get; set; } = 0;
}
