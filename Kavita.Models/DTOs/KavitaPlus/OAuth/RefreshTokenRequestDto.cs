namespace Kavita.Models.DTOs.KavitaPlus.OAuth;

public sealed record RefreshTokenRequestDto
{
    public required OAuthUpstream Upstream { get; set; }
    public required string RefreshToken { get; set; }
}
