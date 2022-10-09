
using API.Entities.Enums;

namespace API.DTOs;

public class UserDto
{
    public string Username { get; init; }
    public string Email { get; init; }
    public string Token { get; set; }
    public string RefreshToken { get; set; }
    public string ApiKey { get; init; }
    public UserPreferencesDto Preferences { get; set; }
    /// <summary>
    /// The highest age rating the user has access to. Not applicable for admins
    /// </summary>
    public AgeRating AgeRestriction { get; set; } = AgeRating.NotApplicable;
}
