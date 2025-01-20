using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Recommendation;
using API.DTOs.SeriesDetail;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using Flurl.Http;
using Hangfire;
using Kavita.Common;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Bcpg.Sig;

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

        var users = await _unitOfWork.UserRepository.GetAllUsersAsync(AppUserIncludes.WantToRead | AppUserIncludes.UserPreferences);
        foreach (var user in users.Where(u => u.UserPreferences.WantToReadSync))
        {
            if (string.IsNullOrEmpty(user.MalUserName) && string.IsNullOrEmpty(user.AniListAccessToken)) continue;

            try
            {
                _logger.LogInformation("Syncing want to read for user: {UserName}", user.UserName);
                var wantToReadSeries =
                    await (
                            $"{Configuration.KavitaPlusApiUrl}/api/metadata/v2/want-to-read?malUsername={user.MalUserName}&aniListToken={user.AniListAccessToken}")
                        .WithKavitaPlusHeaders(license)
                        .WithTimeout(
                            TimeSpan.FromSeconds(120)) // Give extra time as MAL + AniList can result in a lot of data
                        .GetJsonAsync<List<ExternalSeriesDetailDto>>();

                // Match the series (note: There may be duplicates in the final result)
                foreach (var unmatchedSeries in wantToReadSeries)
                {
                    var match = await _unitOfWork.SeriesRepository.MatchSeries(unmatchedSeries);
                    if (match == null)
                    {
                        continue;
                    }

                    // There is a match, add it
                    user.WantToRead.Add(new AppUserWantToRead()
                    {
                        SeriesId = match.Id,
                    });
                    _logger.LogDebug("Added {MatchName} ({Format}) to Want to Read", match.Name, match.Format);
                }

                // Remove existing Want to Read that are duplicates
                user.WantToRead = user.WantToRead.DistinctBy(d => d.SeriesId).ToList();

                // TODO: Need to write in the history table the last sync time

                // Save the left over entities
                _unitOfWork.UserRepository.Update(user);
                await _unitOfWork.CommitAsync();

                // Trigger CleanupService to cleanup any series in WantToRead that don't belong
                RecurringJob.TriggerJob(TaskScheduler.RemoveFromWantToReadTaskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an exception when processing want to read series sync for {User}", user.UserName);
            }
        }

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
