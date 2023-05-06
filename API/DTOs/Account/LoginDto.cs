namespace API.DTOs.Account;

public class LoginDto
{
    public string Username { get; init; } = default!;
    public string Password { get; set; } = default!;
}
