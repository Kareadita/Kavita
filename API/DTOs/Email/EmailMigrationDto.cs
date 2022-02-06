namespace API.DTOs.Email;

public class EmailMigrationDto
{
    public string EmailAddress { get; init; }
    public string Username { get; init; }
    public string ServerConfirmationLink { get; init; }
    /// <summary>
    /// InstallId of this Kavita Instance
    /// </summary>
    public string InstallId { get; init; }
}
