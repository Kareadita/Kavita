namespace API.DTOs.KavitaPlus.License;

public class EncryptLicenseDto
{
    public required string License { get; set; }
    public required string InstallId { get; set; }
    public required string EmailId { get; set; }
    public string? DiscordId { get; set; }
}
