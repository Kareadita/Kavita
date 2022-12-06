namespace API.DTOs.Account;

public class MigrateUserEmailDto
{
    public string Email { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string Password { get; set; } = default!;
}
