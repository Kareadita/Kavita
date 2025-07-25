namespace API.DTOs.Account;

public sealed record MigrateUserEmailDto
{
    public string Email { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string Password { get; set; } = default!;
}
