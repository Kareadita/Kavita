
namespace API.DTOs
{
    public class UserDto
    {
        public string Username { get; init; }
        public string Email { get; init; }
        public string Token { get; init; }
        public string RefreshToken { get; init; }
        public string ApiKey { get; init; }
        public UserPreferencesDto Preferences { get; set; }
    }
}
