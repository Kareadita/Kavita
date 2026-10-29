using System;
using System.Collections.Generic;

namespace Kavita.Models.Attributes;
#nullable enable

/// <summary>
/// For a JSON column whose type depends on other columns of the same row, like an audit payload keyed by its event type.
/// Named by <see cref="ExportScrubAttribute.JsonTypeSelector"/>, and needs a parameterless constructor
/// </summary>
public interface IExportJsonTypeSelector
{
    /// <summary>
    /// The entity properties whose values pick the type, in the order <see cref="TypesFor"/> receives them
    /// </summary>
    IReadOnlyList<string> Discriminators { get; }

    /// <summary>
    /// Every type a value can hold
    /// </summary>
    IReadOnlyList<Type> AllTypes { get; }

    /// <param name="values">The <see cref="Discriminators"/> as stored in the database: an enum is its number, <c>"61"</c></param>
    /// <returns>The types the value can be, tried in turn. Empty when none is known</returns>
    IReadOnlyList<Type> TypesFor(IReadOnlyList<string?> values);
}
