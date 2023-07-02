namespace API.DTOs.License;

public class UpdateLicenseDto
{
    /// <summary>
    /// License Key received from KavitaPlus
    /// </summary>
    public required string License { get; set; }
    /// <summary>
    /// Email registered with Stripe
    /// </summary>
    public required string Email { get; set; }
}
