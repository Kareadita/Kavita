using System.Text.Json.Serialization;

namespace Kavita.Models.DTOs.Kobo;

public class KoboDeviceAuthRequestDto
{
    [JsonPropertyName("UserKey")]
    public string? UserKey { get; set; }
}
