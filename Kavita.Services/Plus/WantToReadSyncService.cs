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
using Kavita.Models.Entities.Enums.Audit;
using Kavita.Models.DTOs.KavitaPlus.Audit;
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
    ILicenseService licenseService,
    IKavitaPlusAuditService auditService,
    IKavitaPlusApiService kavitaPlusApiService)
    : IWantToReadSyncService
{
    public async Task Sync(CancellationToken ct = default)
    {
        if (!await licenseService.HasActiveLicense(ct: ct)) return;

        var license = (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct)).Value;

        var users = await unitOfWork.UserRepository.GetAllUsersAsync(AppUserIncludes.WantToRead | AppUserIncludes.UserPreferences, ct: ct);
        foreach (var user in users)
        {
            logger.LogInformation("Syncing want to read for user: {UserName}", user.UserName);

            var userScrobbleProviders = user.ScrobbleProviders
                .Where(kv => kv.Value.Settings.WantToReadSync)
                .ToList();

            await auditService.LogAsync(
                KavitaPlusAuditCategory.Sync,
                KavitaPlusEventType.SyncStarted,
                AuditStatus.Info,
                userId: user.Id,
                payload: new AuditLogWantToReadSyncParamsDto { UserName = user.UserName, Providers = userScrobbleProviders.Select(kv => kv.Key).ToList()},
                ct: ct);

            var externalSeries = new List<ExternalSeriesDetailDto>();

            foreach (var kv in userScrobbleProviders)
            {
                var token = kv.Value.AuthenticationToken;
                if (string.IsNullOrEmpty(token))
                {
                    logger.LogDebug("Cannot sync Want To Read for user {UserName} for {Provider} as they do not have a valid token", user.UserName, kv.Key);
                    continue;
                }

                var result = await kavitaPlusApiService.GetWantToRead(kv.Key, token, license, ct);
                if (!result.IsSuccess)
                {
                    await auditService.LogAsync(
                        KavitaPlusAuditCategory.Sync,
                        KavitaPlusEventType.SyncFailed,
                        AuditStatus.Failure,
                        userId: user.Id,
                        payload: new AuditLogWantToReadSyncParamsDto { UserName = user.UserName },
                        error: result.ErrorMessage,
                        ct: ct);

                    logger.LogError("Failed to retrieve Want To Read for user {UserName} from {Provider}: {Error}", user.UserName, kv.Key, result.ErrorMessage);
                    continue;
                }

                externalSeries.AddRange(result.Data ?? []);
            }

            foreach (var unmatchedSeries in externalSeries)
            {
                var match = await unitOfWork.SeriesRepository.MatchSeriesAsync(unmatchedSeries, ct);
                if (match == null)
                {
                    continue;
                }

                user.WantToRead.Add(new AppUserWantToRead
                {
                    SeriesId = match.Id,
                });

                logger.LogTrace("Added {MatchName} ({Format}) to Want to Read", match.Name, match.Format);
            }

            user.WantToRead = user.WantToRead.DistinctBy(d => d.SeriesId).ToList();

            unitOfWork.UserRepository.Update(user);
            await unitOfWork.CommitAsync(ct);

            await auditService.LogAsync(
                KavitaPlusAuditCategory.Sync,
                KavitaPlusEventType.SyncCompleted,
                AuditStatus.Success,
                userId: user.Id,
                payload: new AuditLogWantToReadSyncCompletedParamsDto
                {
                    UserName = user.UserName,
                    SeriesMatched = user.WantToRead.Count,
                    Providers = userScrobbleProviders.Select(kv => kv.Key).ToList()
                },
                ct: ct);

            RecurringJob.TriggerJob(TaskScheduler.RemoveFromWantToReadTaskId);
        }

    }
}
