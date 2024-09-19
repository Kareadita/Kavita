using System.ComponentModel.DataAnnotations;

namespace API.DTOs.Account;

public class ConfirmEmailDto
{
    [Required]
    public string Email { get; set; } = default!;
    [Required]
    public string Token { get; set; } = default!;
    [Required]
    [StringLength(256, MinimumLength = 6)]
    public string Password { get; set; } = default!;
    [Required]
    public string Username { get; set; } = default!;
}
