using System.Collections.Generic;

namespace Kavita.Models.DTOs.ReadingLists.CBL.Internal;

/// <summary>
/// Unified reading list model produced by parsing either a V1 XML or V2 JSON CBL file
/// </summary>
public sealed record ParsedCblReadingList
{
    /// <summary>
    /// Unique file identifier
    /// </summary>
    /// <remarks>V2 only - empty for V1</remarks>
    public string Uuid { get; set; } = string.Empty;
    /// <summary>
    /// CBL schema version (1 for XML, 2+ for JSON)
    /// </summary>
    public int SchemaVersion { get; set; } = 1;
    /// <summary>
    /// Display name of the reading list
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Human-readable summary or description
    /// </summary>
    public string Summary { get; set; } = string.Empty;
    /// <summary>
    /// Free-form notes
    /// </summary>
    /// <remarks>V2 only - empty for V1</remarks>
    public string Notes { get; set; } = string.Empty;
    /// <summary>
    /// Start year of the reading list. -1 if not specified
    /// </summary>
    public int StartYear { get; set; } = -1;
    /// <summary>
    /// Start month. V1 only - -1 if not specified
    /// </summary>
    public int StartMonth { get; set; } = -1;
    /// <summary>
    /// End year of the reading list. -1 if not specified
    /// </summary>
    public int EndYear { get; set; } = -1;
    /// <summary>
    /// End month
    /// </summary>
    /// <remarks>V1 only - -1 if not specified.</remarks>
    public int EndMonth { get; set; } = -1;
    /// <summary>
    /// Primary publisher
    /// </summary>
    /// <remarks>V2 only - empty for V1.</remarks>
    public string Publisher { get; set; } = string.Empty;
    /// <summary>
    /// Publisher imprint
    /// </summary>
    /// <remarks>V2 only - empty for V1.</remarks>
    public string Imprint { get; set; } = string.Empty;
    /// <summary>
    /// Classification of the list (master, character, story, etc.)
    /// </summary>
    /// <remarks>V2 only</remarks>
    public CblListType ListType { get; set; } = CblListType.Unknown;
    /// <summary>
    /// User-defined tags
    /// </summary>
    /// <remarks>V2 only</remarks>
    public List<string> Tags { get; set; } = new();
    /// <summary>
    /// Cover image URLs
    /// </summary>
    /// <remarks>V2 only</remarks>
    public List<string> CoverImageUrls { get; set; } = new();
    /// <summary>
    /// Related reading lists
    /// </summary>
    /// <remarks>V2 only</remarks>
    public List<CblRelationship> Relationships { get; set; } = new();
    /// <summary>
    /// External sources the list was derived from
    /// </summary>
    /// <remarks>V2 only</remarks>
    public List<CblSource> Sources { get; set; } = new();
    /// <summary>
    /// Ordered list of issues/books in the reading list.
    /// </summary>
    public List<ParsedCblItem> Items { get; set; } = new();
}
