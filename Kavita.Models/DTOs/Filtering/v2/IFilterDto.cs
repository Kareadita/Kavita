using System.Collections.Generic;

namespace Kavita.Models.DTOs.Filtering.v2;
#nullable enable


/// <summary>
/// Base filter interface for statement processing. Used by <c>FilterQueryBuilder</c>
/// </summary>
public interface IFilterDto<TStatement>
{
    ICollection<TStatement> Statements { get; set; }
    FilterCombination Combination { get; set; }
    int LimitTo { get; set; }
    FilterEntityType EntityType { get; }
}

/// <summary>
/// Represents a filter DTO containing statements, a combination mode, and a limit.
/// Sorting is intentionally excluded, sort field enums differ per entity and sorting
/// is always applied separately in the caller.
/// </summary>
/// <typeparam name="TStatement">The statement type</typeparam>
/// <typeparam name="TSortOption">The sort option type</typeparam>
public interface IFilterDto<TStatement, TSortOption> : IFilterDto<TStatement>
{
    int Id { get; set; }
    string? Name { get; set; }
    ICollection<TStatement> Statements { get; set;  }
    FilterCombination Combination { get; set;  }
    FilterEntityType EntityType { get; }
    TSortOption? SortOptions { get; set; }
    int LimitTo { get; set;  }
}
