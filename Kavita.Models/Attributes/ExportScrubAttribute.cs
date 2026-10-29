using System;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.Attributes;

/// <summary>
/// Marks a value the debug export must not ship as is. Anything without it has to be on the export's reviewed Keep list
/// </summary>
/// <param name="kind">What replaces the value</param>
/// <param name="jsonType">For <see cref="ExportScrubKind.Json"/> on a string that holds JSON: the type it deserializes to</param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class ExportScrubAttribute(ExportScrubKind kind, Type jsonType = null) : Attribute
{
    public ExportScrubKind Kind { get; } = kind;
    public Type JsonType { get; } = jsonType;
}
