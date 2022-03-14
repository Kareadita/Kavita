using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace API.DTOs.Account;

public class InviteUserDto
{
    [Required]
    public string Email { get; init; }
    /// <summary>
    /// List of Roles to assign to user. If admin not present, Pleb will be applied.
    /// If admin present, all libraries will be granted access and will ignore those from DTO.
    /// </summary>
    public ICollection<string> Roles { get; init; }
    /// <summary>
    /// A list of libraries to grant access to
    /// </summary>
    public IList<int> Libraries { get; init; }
}
