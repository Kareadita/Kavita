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

/// <summary>
/// The <c>fileDetails</c> block — identifies the file with a UUID and schema version.
/// </summary>
public sealed class CblV2FileDetails
{
    /// <summary>
    /// Unique identifier for this CBL file
    /// </summary>
    public string UUID { get; set; }
    /// <summary>
    /// Schema version number (e.g. 1.0)
    /// </summary>
    [JsonPropertyName("version")]
    public double? Version { get; set; }
}

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

/// <summary>
/// An entry in <c>listDetails.relationships[]</c> — links to a related reading list.
/// </summary>
public sealed class CblV2Relationship
{
    /// <summary>
    /// Display name of the related reading list
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }
    /// <summary>
    /// UUID of the related reading list file
    /// </summary>
    public string UUID { get; set; }
    /// <summary>
    /// Nature of the relationship (e.g. "prequel", "sequel", "companion")
    /// </summary>
    [JsonPropertyName("relationship")]
    public string Relationship { get; set; }
}

/// <summary>
/// An entry in <c>listDetails.source[]</c> — origin of the reading list data
/// </summary>
public sealed class CblV2Source
{
    /// <summary>
    /// Name of the source (e.g. "Comic Book Herald")
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }
    /// <summary>
    /// URL of the source
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; }
}

/// <summary>
/// An entry in <c>issueList[]</c> — a single issue in the reading list
/// </summary>
public sealed class CblV2Issue
{
    /// <summary>
    /// Name of the comic series
    /// </summary>
    [JsonPropertyName("seriesName")]
    public string SeriesName { get; set; }
    /// <summary>
    /// Year the series started (used to disambiguate reboots)
    /// </summary>
    [JsonPropertyName("seriesStartYear")]
    public int? SeriesStartYear { get; set; }
    /// <summary>
    /// Display issue number (e.g. "1", "Annual 2")
    /// </summary>
    [JsonPropertyName("issueNumber")]
    public string IssueNumber { get; set; }
    /// <summary>
    /// Cover date in ISO 8601 format (YYYY-MM-DD)
    /// </summary>
    [JsonPropertyName("issueCoverDate")]
    public string IssueCoverDate { get; set; }
    /// <summary>
    /// Categorisation of the issue (e.g. "event-core", "ongoing")
    /// </summary>
    [JsonPropertyName("issueType")]
    public string IssueType { get; set; }
    /// <summary>
    /// External database identifiers for this issue
    /// </summary>
    [JsonPropertyName("id")]
    public List<CblV2ExternalId> Id { get; set; }
}

/// <summary>
/// An entry in <c>issueList[].id[]</c> — external database reference for an issue.
/// </summary>
public sealed class CblV2ExternalId
{
    /// <summary>
    /// Provider short-name (e.g. "cv", "metron", "gcd")
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }
    /// <summary>
    /// The provider's series identifier
    /// </summary>
    [JsonPropertyName("series")]
    public string Series { get; set; }
    /// <summary>
    /// The provider's issue identifier
    /// </summary>
    [JsonPropertyName("issue")]
    public string Issue { get; set; }
}
