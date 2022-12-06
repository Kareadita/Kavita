namespace API.DTOs.Email;

public class ConfirmationEmailDto
{
    public string InvitingUser { get; init; } = default!;
    public string EmailAddress { get; init; } = default!;
    public string ServerConfirmationLink { get; init; } = default!;
    /// <summary>
    /// InstallId of this Kavita Instance
    /// </summary>
    public string InstallId { get; init; } = default!;
}
