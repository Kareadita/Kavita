using System.ComponentModel.DataAnnotations;

namespace API.DTOs;

public class RegisterDto
{
    [Required]
    public string Username { get; init; }
    /// <summary>
    /// An email to register with. Optional. Provides Forgot Password functionality
    /// </summary>
    public string Email { get; init; }
    [Required]
    [StringLength(32, MinimumLength = 6)]
    public string Password { get; set; }
}
