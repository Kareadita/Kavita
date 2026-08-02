using System.Text.Json.Serialization;

namespace Kavita.Models.DTOs.Kobo;

/// <summary>
/// Dummy bearer exchange payload matching Calibre-Web's auth/device and auth/refresh responses.
/// </summary>
public class KoboAuthTokenDto
{
    [JsonPropertyName("AccessToken")]
    public required string AccessToken { get; init; }

    [JsonPropertyName("RefreshToken")]
    public required string RefreshToken { get; init; }

    [JsonPropertyName("TokenType")]
    public string TokenType { get; init; } = "Bearer";

    [JsonPropertyName("TrackingId")]
    public required string TrackingId { get; init; }

    [JsonPropertyName("UserKey")]
    public string UserKey { get; init; } = string.Empty;
}
