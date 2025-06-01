using System.ComponentModel.DataAnnotations;

namespace API.DTOs.Account;

public sealed record ConfirmEmailUpdateDto
{
    [Required]
    public string Email { get; set; } = default!;
    [Required]
    public string Token { get; set; } = default!;
}
