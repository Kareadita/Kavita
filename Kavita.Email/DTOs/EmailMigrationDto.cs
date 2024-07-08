namespace Skeleton.DTOs;

public class EmailMigrationDto
{
    public string EmailAddress { get; init; }
    public string ServerConfirmationLink { get; init; }
    public string Username { get; init; }
    public string InstallId { get; init; }
}