using System;

namespace Kavita.Models.DTOs.Filtering.v2;

/// <summary>
/// Represents a single filter statement with a field enum, comparison operator, and raw string value.
/// </summary>
/// <typeparam name="TField">The field enum type (FilterField, PersonFilterField, etc.)</typeparam>
public interface IFilterStatement<TField> where TField : Enum
{
    FilterComparison Comparison { get; set; }
    TField Field { get; set; }
    string Value { get; set; }
}
