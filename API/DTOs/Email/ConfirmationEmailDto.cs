namespace API.DTOs.Email;

public class ConfirmationEmailDto
{
    public string InvitingUser { get; init; }
    public string EmailAddress { get; init; }
    public string ServerConfirmationLink { get; init; }
}
