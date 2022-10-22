using System.ComponentModel.DataAnnotations;

namespace API.DTOs.Account;

public class ConfirmEmailUpdateDto
{
    [Required]
    public string Email { get; set; }
    [Required]
    public string Token { get; set; }
}
