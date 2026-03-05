using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.KavitaPlus.License;

namespace Kavita.API.Services.Plus;

public interface ILicenseService
{
    //Task ValidateLicenseStatus();
    Task RemoveLicense(CancellationToken ct = default);
    Task AddLicense(string license, string email, string? discordId, CancellationToken ct = default);
    Task<bool> HasActiveLicense(bool forceCheck = false, CancellationToken ct = default);
    Task<bool> HasActiveSubscription(string? license, CancellationToken ct = default);
    Task<bool> ResetLicense(string license, string email, CancellationToken ct = default);
    Task<LicenseInfoDto?> GetLicenseInfo(bool forceCheck = false, CancellationToken ct = default);
    Task<bool> ResendWelcomeEmail(CancellationToken ct = default);
}
