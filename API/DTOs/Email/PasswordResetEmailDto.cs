namespace API.DTOs.Email;

public class PasswordResetEmailDto
{
    public string EmailAddress { get; init; }
    public string ServerConfirmationLink { get; init; }
}
