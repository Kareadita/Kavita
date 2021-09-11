using System.ComponentModel.DataAnnotations;

namespace API.DTOs.Account
{
    public class ResetPasswordDto
    {
        [Required]
        public string UserName { get; init; }
        [Required]
        [StringLength(32, MinimumLength = 6)]
        public string Password { get; init; }
    }
}
