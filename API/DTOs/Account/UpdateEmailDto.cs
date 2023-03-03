namespace API.DTOs.Account;

public class UpdateEmailDto
{
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
}
