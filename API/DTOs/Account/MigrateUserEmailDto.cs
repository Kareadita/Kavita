using System.ComponentModel.DataAnnotations;

namespace API.DTOs.Account;

public class MigrateUserEmailDto
{
    public string Email { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public bool SendEmail { get; set; }
}
