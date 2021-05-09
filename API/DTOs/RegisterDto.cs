using System.ComponentModel.DataAnnotations;

namespace API.DTOs
{
    public class RegisterDto
    {
        [Required]
        public string Username { get; init; }
        [Required]
        [StringLength(32, MinimumLength = 4)]
        public string Password { get; init; }
        public bool IsAdmin { get; init; }
    }
}