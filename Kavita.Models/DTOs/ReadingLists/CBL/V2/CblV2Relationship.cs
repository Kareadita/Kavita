using System.Text.Json.Serialization;

namespace Kavita.Models.DTOs.ReadingLists.CBL.V2;


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
