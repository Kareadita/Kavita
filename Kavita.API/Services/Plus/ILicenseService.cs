using System.Threading.Tasks;
using Kavita.Models.DTOs.KavitaPlus.License;

namespace Kavita.API.Services.Plus;

public interface ILicenseService
{
    //Task ValidateLicenseStatus();
    Task RemoveLicense();
    Task AddLicense(string license, string email, string? discordId);
    Task<bool> HasActiveLicense(bool forceCheck = false);
    Task<bool> HasActiveSubscription(string? license);
    Task<bool> ResetLicense(string license, string email);
    Task<LicenseInfoDto?> GetLicenseInfo(bool forceCheck = false);
    Task<bool> ResendWelcomeEmail();
}
