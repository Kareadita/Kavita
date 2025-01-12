using System;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.SeriesDetail;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using Flurl.Http;
using Kavita.Common;
using Microsoft.Extensions.Logging;

namespace API.Services.Plus;


public interface IWantToReadSyncService
{
    Task Sync();
}

/// <summary>
/// Responsible for syncing Want To Read from upstream providers with Kavita
/// </summary>
public class WantToReadSyncService : IWantToReadSyncService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<WantToReadSyncService> _logger;
    private readonly ILicenseService _licenseService;

    public WantToReadSyncService(IUnitOfWork unitOfWork, ILogger<WantToReadSyncService> logger, ILicenseService licenseService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _licenseService = licenseService;
    }

    public async Task Sync()
    {
        if (!await _licenseService.HasActiveLicense()) return;

        var license = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey)).Value;

        var wantToReadSeries = await ($"{Configuration.KavitaPlusApiUrl}/api/metadata/v2/want-to-read")
            .WithKavitaPlusHeaders(license)
            .GetJsonAsync<SeriesDetailPlusDto>();

        // Match the series (like ScrobbleService does) to actual Kavita instances

        // Remove existing Want to Read or any Series with full completion and Publisher status of Completed

        // Save the left over entities


        throw new System.NotImplementedException();
    }

    // Allow syncing if there are any libraries that have an appropriate Provider, the user has the appropriate token, and the last Sync validates
    // private async Task<bool> CanSync(AppUser? user)
    // {
    //
    //     if (collection is not {Source: ScrobbleProvider.Mal}) return false;
    //     if (string.IsNullOrEmpty(collection.SourceUrl)) return false;
    //     if (collection.LastSyncUtc.Truncate(TimeSpan.TicksPerHour) >= DateTime.UtcNow.AddDays(SyncDelta).Truncate(TimeSpan.TicksPerHour)) return false;
    //     return true;
    // }
}
