namespace API.DTOs.Email;

public class ConfirmationEmailDto
{
    public string InvitingUser { get; init; }
    public string EmailAddress { get; init; }
    public string ServerConfirmationLink { get; init; }
    /// <summary>
    /// InstallId of this Kavita Instance
    /// </summary>
    public string InstallId { get; init; }
}
