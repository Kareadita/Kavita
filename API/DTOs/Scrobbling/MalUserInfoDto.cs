namespace API.DTOs.Scrobbling;

/// <summary>
/// Information about a User's MAL connection
/// </summary>
public class MalUserInfoDto
{
    public required string Username { get; set; }
    /// <summary>
    /// This is actually the Client Id
    /// </summary>
    public required string AccessToken { get; set; }
}
