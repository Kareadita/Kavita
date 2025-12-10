using System.Collections.Generic;
using API.Entities.Enums;

namespace API.DTOs.Account;
#nullable enable

public sealed record UpdateUserDto
{
    /// <inheritdoc cref="API.Entities.AppUser.Id"/>
    public int UserId { get; set; }
    /// <inheritdoc cref="API.Entities.AppUser.UserName"/>
    public string Username { get; set; } = default!;
    /// <summary>
    /// List of Roles to assign to user. If admin not present, Pleb will be applied.
    /// If admin present, all libraries will be granted access and will ignore those from DTO.
    /// </summary>
    public IList<string> Roles { get; init; } = default!;
    /// <summary>
    /// A list of libraries to grant access to
    /// </summary>
    public IList<int> Libraries { get; init; } = default!;
    /// <summary>
    /// An Age Rating which will limit the account to seeing everything equal to or below said rating.
    /// </summary>
    public AgeRestrictionDto AgeRestriction { get; init; } = default!;
    /// <inheritdoc cref="API.Entities.AppUser.Email"/>
    public string? Email { get; set; } = default!;
    public IdentityProvider IdentityProvider { get; init; } = IdentityProvider.Kavita;
}
