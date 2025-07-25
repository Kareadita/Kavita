namespace API.DTOs.Account;

public sealed record UpdateEmailDto
{
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
}
