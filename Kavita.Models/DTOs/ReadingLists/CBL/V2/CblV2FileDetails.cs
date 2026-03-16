using System.Text.Json.Serialization;

namespace Kavita.Models.DTOs.ReadingLists.CBL.V2;


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
