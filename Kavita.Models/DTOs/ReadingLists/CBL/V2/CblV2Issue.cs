using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Kavita.Models.DTOs.ReadingLists.CBL.V2;


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
