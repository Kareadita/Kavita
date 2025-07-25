namespace API.DTOs.Scrobbling;
#nullable enable

/// <summary>
/// Response from Kavita+ Scrobble API
/// </summary>
public sealed record ScrobbleResponseDto
{
    public bool Successful { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ExtraInformation  {get; set;}
    public int RateLeft { get; set; }
}
