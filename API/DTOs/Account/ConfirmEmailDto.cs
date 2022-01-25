using System.ComponentModel.DataAnnotations;

namespace API.DTOs.Account;

public class ConfirmEmailDto
{
    [Required]
    public string Email { get; set; }
    [Required]
    public string Token { get; set; }
    [Required]
    [StringLength(32, MinimumLength = 6)]
    public string Password { get; set; }
    [Required]
    public string Username { get; set; }
}
