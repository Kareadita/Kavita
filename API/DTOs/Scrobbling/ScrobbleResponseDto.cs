namespace API.DTOs.Scrobbling;

/// <summary>
/// Response from Kavita+ Scrobble API
/// </summary>
public class ScrobbleResponseDto
{
    public bool Successful { get; set; }
    public string? ErrorMessage { get; set; }
    public int RateLeft { get; set; }
}
