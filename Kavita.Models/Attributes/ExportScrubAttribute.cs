using System;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.Attributes;

/// <summary>
/// Marks a value the debug export must not ship as is. Anything without it is exported unchanged
/// </summary>
/// <param name="kind">What replaces the value</param>
/// <param name="jsonType">For <see cref="ExportScrubKind.Json"/> on a string that holds JSON: the type it deserializes to</param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class ExportScrubAttribute(ExportScrubKind kind, Type jsonType = null) : Attribute
{
    public ExportScrubKind Kind { get; } = kind;
    public Type JsonType { get; } = jsonType;

    /// <summary>
    /// For <see cref="ExportScrubKind.Json"/> when the type depends on the row: an <see cref="IExportJsonTypeSelector"/>, used instead of <see cref="JsonType"/>
    /// </summary>
    /// <remarks>For example, KavitaPlusAudit Json columns</remarks>
    public Type JsonTypeSelector { get; set; }
}
