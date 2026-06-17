namespace Kavita.Models.DTOs.KavitaPlus.OAuth;

public class RefreshTokenRequestDto
{
    public required OAuthUpstream Upstream { get; set; }
    public required string RefreshToken { get; set; }
}
