namespace API.DTOs.Account;

public sealed record ConfirmMigrationEmailDto
{
    public string Email { get; set; } = default!;
    public string Token { get; set; } = default!;
}
