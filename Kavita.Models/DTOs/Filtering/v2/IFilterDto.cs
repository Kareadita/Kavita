using System;
using System.Collections.Generic;

namespace Kavita.Models.DTOs.Filtering.v2;

/// <summary>
/// Represents a single filter statement with a field enum, comparison operator, and raw string value.
/// </summary>
/// <typeparam name="TField">The field enum type (FilterField, PersonFilterField, etc.)</typeparam>
public interface IFilterStatement<out TField> where TField : Enum
{
    FilterComparison Comparison { get; }
    TField Field { get; }
    string Value { get; }
}

/// <summary>
/// Represents a filter DTO containing statements, a combination mode, and a limit.
/// Sorting is intentionally excluded — sort field enums differ per entity and sorting
/// is always applied separately in the caller.
/// </summary>
/// <typeparam name="TStatement">The statement type</typeparam>
public interface IFilterDto<TStatement>
{
    ICollection<TStatement> Statements { get; }
    FilterCombination Combination { get; }
    int LimitTo { get; }
}
