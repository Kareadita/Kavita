namespace API.DTOs.Email;

public class EmailMigrationDto
{
    public string EmailAddress { get; init; } = default!;
    public string Username { get; init; } = default!;
    public string ServerConfirmationLink { get; init; } = default!;
    /// <summary>
    /// InstallId of this Kavita Instance
    /// </summary>
    public string InstallId { get; init; } = default!;
}
