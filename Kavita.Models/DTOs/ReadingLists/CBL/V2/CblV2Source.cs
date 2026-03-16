using System.Text.Json.Serialization;

namespace Kavita.Models.DTOs.ReadingLists.CBL.V2;

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
