using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.KavitaPlus.License;

namespace Kavita.API.Services.Plus;

public interface ILicenseService
{
    Task RemoveLicense(CancellationToken ct = default);
    Task<KavitaPlusRegisterResultDto> AddLicense(string license, string email, string? discordId, CancellationToken ct = default);
    Task<bool> HasActiveLicense(bool forceCheck = false, CancellationToken ct = default);
    Task<bool> HasActiveSubscription(string? license, CancellationToken ct = default);
    Task<bool> ResetLicense(string license, string email, CancellationToken ct = default);
    Task<LicenseInfoDto?> GetLicenseInfo(bool forceCheck = false, CancellationToken ct = default);
    Task<bool> ResendWelcomeEmail(CancellationToken ct = default);
    Task<KavitaPlusLicenseUsageDto> GetLicenseUsage(CancellationToken ct = default);
    Task<bool> CancelLicense(CancelKavitaPlusLicenseDto dto, CancellationToken ct);
    Task<IList<KavitaPlusProductInfoDto>> GetProducts(CancellationToken ct = default);
    Task<string?> RenewLicense(RenewKavitaPlusLicenseDto dto, CancellationToken ct);
    Task<bool> ChangeEmail(ChangeEmailOnLicenseDto dto, CancellationToken ct);
}
