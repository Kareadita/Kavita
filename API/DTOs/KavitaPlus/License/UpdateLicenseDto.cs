namespace API.DTOs.KavitaPlus.License;

public class UpdateLicenseDto
{
    /// <summary>
    /// License Key received from Kavita+
    /// </summary>
    public required string License { get; set; }
    /// <summary>
    /// Email registered with Stripe
    /// </summary>
    public required string Email { get; set; }
    /// <summary>
    /// Optional DiscordId
    /// </summary>
    public string? DiscordId { get; set; }
}
