
using System;
using System.Collections.Generic;
using API.DTOs.Account;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Enums.User;
using API.Entities.Interfaces;
using NotImplementedException = System.NotImplementedException;

namespace API.DTOs;
#nullable enable

public sealed record UserDto : IHasCoverImage
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
    public DateTime Created { get; set; }
    public DateTime CreatedUtc { get; set; }
    /// <summary>
    /// Only System-provided Auth Keys
    /// </summary>
    public ICollection<AuthKeyDto> AuthKeys { get; set; }

    public string? CoverImage { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public void ResetColorScape()
    {
        PrimaryColor = string.Empty;
        SecondaryColor = string.Empty;
    }
}
