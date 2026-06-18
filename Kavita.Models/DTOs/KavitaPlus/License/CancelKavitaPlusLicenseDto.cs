namespace Kavita.Models.DTOs.KavitaPlus.License;
#nullable enable

public sealed record CancelKavitaPlusLicenseDto
{
    public required string Email { get; set; }
    /// <summary>
    /// Optional comment to tell why you cancelled
    /// </summary>
    public string? Comment  { get; set; }
}
