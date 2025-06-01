namespace API.DTOs.Account;

public sealed record TokenRequestDto
{
    public string Token { get; init; } = default!;
    public string RefreshToken { get; init; } = default!;
}
