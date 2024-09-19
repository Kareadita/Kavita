using System.ComponentModel.DataAnnotations;

namespace API.DTOs;

public class RegisterDto
{
    [Required]
    public string Username { get; init; } = default!;
    /// <summary>
    /// An email to register with. Optional. Provides Forgot Password functionality
    /// </summary>
    public string Email { get; init; } = default!;
    [Required]
    [StringLength(256, MinimumLength = 6)]
    public string Password { get; set; } = default!;
}
