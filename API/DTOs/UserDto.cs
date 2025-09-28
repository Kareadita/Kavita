
using System.Collections.Generic;
using API.DTOs.Account;
using API.Entities;
using API.Entities.Enums;

namespace API.DTOs;
#nullable enable

public sealed record UserDto
{
    public int Id { get; init; }
    public string Username { get; init; } = null!;
    public string Email { get; init; } = null!;
    public IList<string> Roles { get; set; } = [];
    public string Token { get; set; } = null!;
    public string? RefreshToken { get; set; }
    public string? ApiKey { get; init; }
    public UserPreferencesDto? Preferences { get; set; }
    public AgeRestrictionDto? AgeRestriction { get; init; }
    public string KavitaVersion { get; set; }
    /// <inheritdoc cref="AppUser.IdentityProvider"/>
    public IdentityProvider IdentityProvider { get; init; }
}
