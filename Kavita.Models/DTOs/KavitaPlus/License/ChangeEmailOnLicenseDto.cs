namespace Kavita.Models.DTOs.KavitaPlus.License;

public sealed record ChangeEmailOnLicenseDto
{
    public required string OldEmail { get; set; }
    public required string NewEmail { get; set; }

}
