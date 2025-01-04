namespace API.DTOs.KavitaPlus.License;

public class ResetLicenseDto
{
    public required string License { get; set; }
    public required string InstallId { get; set; }
    public required string EmailId { get; set; }
}
