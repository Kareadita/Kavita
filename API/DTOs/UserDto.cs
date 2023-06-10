
using API.DTOs.Account;

namespace API.DTOs;

public class UserDto
{
    public string Username { get; init; } = null!;
    public string Email { get; init; } = null!;
    public string Token { get; set; } = null!;
    public string? RefreshToken { get; set; }
    public string? ApiKey { get; init; }
    public UserPreferencesDto? Preferences { get; set; }
    public AgeRestrictionDto? AgeRestriction { get; init; }
    public string KavitaVersion { get; set; }
    public bool HasLicense { get; set; }
}
