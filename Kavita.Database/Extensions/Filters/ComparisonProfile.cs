using System;
using System.Collections.Generic;
using Kavita.Common;
using Kavita.Models.DTOs.Filtering.v2;

namespace Kavita.Database.Extensions.Filters;

/// <summary>
/// Defines which <see cref="FilterComparison"/> values are valid for a given filter shape.
/// Call <see cref="Validate"/> at the top of each filter method to reject unsupported comparisons
/// without needing exhaustive switch arms.
/// </summary>
public static class ComparisonProfile
{
    /// <summary>
    /// String fields: Equal, NotEqual, BeginsWith, EndsWith, Matches
    /// </summary>
    public static readonly HashSet<FilterComparison> String =
    [
        FilterComparison.Equal, FilterComparison.NotEqual,
        FilterComparison.BeginsWith, FilterComparison.EndsWith, FilterComparison.Matches
    ];

    /// <summary>
    /// Numeric fields: Equal, NotEqual, GreaterThan, GreaterThanEqual, LessThan, LessThanEqual
    /// </summary>
    public static readonly HashSet<FilterComparison> Numeric =
    [
        FilterComparison.Equal, FilterComparison.NotEqual,
        FilterComparison.GreaterThan, FilterComparison.GreaterThanEqual,
        FilterComparison.LessThan, FilterComparison.LessThanEqual
    ];

    /// <summary>
    /// List/set membership fields: Equal, NotEqual, Contains, NotContains, MustContains
    /// </summary>
    public static readonly HashSet<FilterComparison> List =
    [
        FilterComparison.Equal, FilterComparison.NotEqual,
        FilterComparison.Contains, FilterComparison.NotContains, FilterComparison.MustContains
    ];

    /// <summary>
    /// List/set membership fields with IsEmpty: Equal, NotEqual, Contains, NotContains, MustContains, IsEmpty, IsNotEmpty
    /// </summary>
    public static readonly HashSet<FilterComparison> ListWithEmpty =
    [
        FilterComparison.Equal, FilterComparison.NotEqual,
        FilterComparison.Contains, FilterComparison.NotContains, FilterComparison.MustContains,
        FilterComparison.IsEmpty, FilterComparison.IsNotEmpty
    ];

    /// <summary>
    /// Date fields: Numeric comparisons plus IsBefore, IsAfter, IsInLast, IsNotInLast, IsEmpty
    /// </summary>
    public static readonly HashSet<FilterComparison> Date =
    [
        FilterComparison.Equal, FilterComparison.NotEqual,
        FilterComparison.GreaterThan, FilterComparison.GreaterThanEqual,
        FilterComparison.LessThan, FilterComparison.LessThanEqual,
        FilterComparison.IsBefore, FilterComparison.IsAfter,
        FilterComparison.IsInLast, FilterComparison.IsNotInLast,
        FilterComparison.IsEmpty, FilterComparison.IsNotEmpty
    ];

    /// <summary>
    /// Throws <see cref="KavitaException"/> if the comparison is not in the allowed set.
    /// </summary>
    /// <param name="comparison">The comparison to validate</param>
    /// <param name="allowed">The set of allowed comparisons for this field</param>
    /// <param name="fieldName">The field name for the error message (e.g. "Person.Name")</param>
    public static void Validate(FilterComparison comparison, HashSet<FilterComparison> allowed, string fieldName)
    {
        if (!allowed.Contains(comparison))
            throw new KavitaException($"{comparison} is not applicable for {fieldName}");
    }
}
