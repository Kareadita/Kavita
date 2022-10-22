using System.ComponentModel.DataAnnotations;

namespace API.DTOs.Account;

public class ResetPasswordDto
{
    /// <summary>
    /// The Username of the User
    /// </summary>
    [Required]
    public string UserName { get; init; }
    /// <summary>
    /// The new password
    /// </summary>
    [Required]
    [StringLength(32, MinimumLength = 6)]
    public string Password { get; init; }
    /// <summary>
    /// The old, existing password. If an admin is performing the change, this is not required. Otherwise, it is.
    /// </summary>
    public string OldPassword { get; init; }
}
