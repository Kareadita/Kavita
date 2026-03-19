using System.Collections.Generic;
using System.Text.Json.Serialization;
using Kavita.Models.DTOs.ReadingLists.CBL.Internal;

namespace Kavita.Models.DTOs.ReadingLists.CBL.V2;


/// <summary>
/// The <c>listDetails</c> block — descriptive metadata for the reading list.
/// </summary>
public sealed class CblV2ListDetails
{
    /// <summary>
    /// Display name of the reading list
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }
    /// <summary>
    /// Human-readable description / summary
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; }
    /// <summary>
    /// Earliest publication year covered by the list
    /// </summary>
    [JsonPropertyName("startYear")]
    public int? StartYear { get; set; }
    /// <summary>
    /// Latest publication year covered by the list
    /// </summary>
    [JsonPropertyName("endYear")]
    public int? EndYear { get; set; }
    /// <summary>
    /// Primary publisher (e.g. "Marvel", "DC")
    /// </summary>
    [JsonPropertyName("publisher")]
    public string Publisher { get; set; }
    /// <summary>
    /// Publisher imprint (e.g. "Vertigo", "Icon")
    /// </summary>
    [JsonPropertyName("imprint")]
    public string Imprint { get; set; }
    /// <summary>
    /// List type as a free-form string (mapped to <see cref="CblListType"/>)
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; }
    /// <summary>
    /// User-defined tags for categorisation
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; }
    /// <summary>
    /// URLs for cover images associated with the list
    /// </summary>
    [JsonPropertyName("coverImageURLs")]
    public List<string> CoverImageURLs { get; set; }
    /// <summary>
    /// Links to related reading lists (prequels, sequels, etc.)
    /// </summary>
    [JsonPropertyName("relationships")]
    public List<CblV2Relationship> Relationships { get; set; }
    /// <summary>
    /// External sources that this list was derived from
    /// </summary>
    [JsonPropertyName("source")]
    public List<CblV2Source> Source { get; set; }
}
