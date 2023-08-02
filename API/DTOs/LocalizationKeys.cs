using System.Text.Json.Serialization;

namespace API.DTOs;

public class LocalizationKeys
{
    [JsonPropertyName("confirm-email")]
    public string ConfirmEmail { get; }
}
