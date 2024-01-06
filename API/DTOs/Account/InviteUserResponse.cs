namespace API.DTOs.Account;

public class InviteUserResponse
{
    /// <summary>
    /// Email link used to setup the user account
    /// </summary>
    public string EmailLink { get; set; } = default!;
    /// <summary>
    /// Was an email sent (ie is this server accessible)
    /// </summary>
    public bool EmailSent { get; set; } = default!;
    /// <summary>
    /// When a user has an invalid email and is attempting to perform a flow.
    /// </summary>
    public bool InvalidEmail { get; set; } = false;
}
