namespace API.DTOs.KavitaPlus.License;
#nullable enable

public sealed record EncryptLicenseDto
{
    public required string License { get; set; }
    public required string InstallId { get; set; }
    public required string EmailId { get; set; }
    public string? DiscordId { get; set; }
}
