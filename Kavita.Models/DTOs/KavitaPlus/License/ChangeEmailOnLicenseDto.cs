namespace Kavita.Models.DTOs.KavitaPlus.License;

public sealed record ChangeEmailOnLicenseDto
{
    public required string ExistingEmail { get; set; }
    public required string NewEmail { get; set; }

}
