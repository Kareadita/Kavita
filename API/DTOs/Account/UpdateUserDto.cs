using System.Collections.Generic;

namespace API.DTOs.Account;

public record UpdateUserDto
{
    public int UserId { get; set; }
    public string Username { get; set; }
    /// List of Roles to assign to user. If admin not present, Pleb will be applied.
    /// If admin present, all libraries will be granted access and will ignore those from DTO.
    public IList<string> Roles { get; init; }
    /// <summary>
    /// A list of libraries to grant access to
    /// </summary>
    public IList<int> Libraries { get; init; }
    /// <summary>
    /// An Age Rating which will limit the account to seeing everything equal to or below said rating.
    /// </summary>
    public AgeRestrictionDto AgeRestriction { get; init; }

}
