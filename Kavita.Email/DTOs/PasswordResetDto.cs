namespace Skeleton.DTOs;

public class PasswordResetDto
{
    public string EmailAddress { get; init; }
    public string ServerConfirmationLink { get; init; }
    public string InstallId { get; init; }
}