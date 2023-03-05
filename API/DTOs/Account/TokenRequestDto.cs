namespace API.DTOs.Account;

public class TokenRequestDto
{
    public string Token { get; init; } = default!;
    public string RefreshToken { get; init; } = default!;
}
