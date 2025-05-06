namespace API.DTOs.KavitaPlus.License;

public sealed record LicenseValidDto
{
    public required string License { get; set; }
    public required string InstallId { get; set; }
}
