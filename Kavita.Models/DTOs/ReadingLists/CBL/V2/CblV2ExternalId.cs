using System.Text.Json.Serialization;

namespace Kavita.Models.DTOs.ReadingLists.CBL.V2;


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
