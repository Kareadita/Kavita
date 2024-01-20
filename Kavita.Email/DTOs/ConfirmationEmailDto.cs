namespace Skeleton.DTOs;

public record ConfirmationEmailDto
{
    public string InvitingUser { get; init; }
    public string EmailAddress { get; init; }
    public string ServerConfirmationLink { get; init; }
    public string InstallId { get; init; }
    
}