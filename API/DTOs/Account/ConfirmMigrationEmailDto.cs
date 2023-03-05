namespace API.DTOs.Account;

public class ConfirmMigrationEmailDto
{
    public string Email { get; set; } = default!;
    public string Token { get; set; } = default!;
}
