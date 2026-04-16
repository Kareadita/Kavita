using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services.Plus;
using Kavita.Common;
using Kavita.Common.Extensions;
using Kavita.Models.DTOs.KavitaPlus.Metadata;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Plus;


/// <summary>
/// Responsible for syncing Want To Read from upstream providers with Kavita
/// </summary>
public class WantToReadSyncService(
    IUnitOfWork unitOfWork,
    ILogger<WantToReadSyncService> logger,
    ILicenseService licenseService)
    : IWantToReadSyncService
{
    public async Task Sync(CancellationToken ct = default)
    {
        if (!await licenseService.HasActiveLicense(ct: ct)) return;

        var license = (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct)).Value;

        var users = await unitOfWork.UserRepository.GetAllUsersAsync(AppUserIncludes.WantToRead | AppUserIncludes.UserPreferences, ct: ct);
        foreach (var user in users.Where(u => u.UserPreferences.WantToReadSync))
        {
            if (string.IsNullOrEmpty(user.MalUserName) && string.IsNullOrEmpty(user.AniListAccessToken)) continue;

            try
            {
                logger.LogInformation("Syncing want to read for user: {UserName}", user.UserName);
                var wantToReadSeries =
                    await (
                            $"{Configuration.KavitaPlusApiUrl}/api/metadata/v2/want-to-read?malUsername={user.MalUserName}&aniListToken={user.AniListAccessToken}")
                        .WithKavitaPlusHeaders(license)
                        .WithTimeout(
                            TimeSpan.FromSeconds(120)) // Give extra time as MAL + AniList can result in a lot of data
                        .GetJsonAsync<List<ExternalSeriesDetailDto>>(cancellationToken: ct);

                // Match the series (note: There may be duplicates in the final result)
                foreach (var unmatchedSeries in wantToReadSeries)
                {
                    var match = await unitOfWork.SeriesRepository.MatchSeriesAsync(unmatchedSeries, ct);
                    if (match == null)
                    {
                        continue;
                    }

                    // There is a match, add it
                    user.WantToRead.Add(new AppUserWantToRead()
                    {
                        SeriesId = match.Id,
                    });
                    logger.LogDebug("Added {MatchName} ({Format}) to Want to Read", match.Name, match.Format);
                }

                // Remove existing Want to Read that are duplicates
                user.WantToRead = user.WantToRead.DistinctBy(d => d.SeriesId).ToList();

                // TODO: Need to write in the history table the last sync time

                // Save the left over entities
                unitOfWork.UserRepository.Update(user);
                await unitOfWork.CommitAsync(ct);

                // Trigger CleanupService to cleanup any series in WantToRead that don't belong
                RecurringJob.TriggerJob(TaskScheduler.RemoveFromWantToReadTaskId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "There was an exception when processing want to read series sync for {User}", user.UserName);
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
