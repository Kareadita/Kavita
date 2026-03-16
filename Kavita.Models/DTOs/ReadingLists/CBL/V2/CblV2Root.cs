using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Kavita.Models.DTOs.ReadingLists.CBL.V2;

/// <summary>
/// Top-level V2 JSON CBL document.
/// </summary>
/// <remarks>https://github.com/ComicReadingLists/json-cbl-standard/blob/main/schema/1.0/comic-reading-list.schema.json</remarks>
public sealed class CblV2Root
{
    /// <summary>
    /// File-level metadata (UUID, schema version)
    /// </summary>
    [JsonPropertyName("fileDetails")]
    public CblV2FileDetails FileDetails { get; set; }
    /// <summary>
    /// Descriptive metadata for the reading list
    /// </summary>
    [JsonPropertyName("listDetails")]
    public CblV2ListDetails ListDetails { get; set; }
    /// <summary>
    /// Ordered list of issues in the reading list
    /// </summary>
    [JsonPropertyName("issueList")]
    public List<CblV2Issue> IssueList { get; set; }
    /// <summary>
    /// Free-form notes about the reading list
    /// </summary>
    [JsonPropertyName("notes")]
    public string Notes { get; set; }
}
