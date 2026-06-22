using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services.Plus;
using Kavita.API.Services.SignalR;
using Kavita.Common;
using Kavita.Common.Extensions;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs.KavitaPlus.Audit;
using Kavita.Models.DTOs.KavitaPlus.ExternalMetadata;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.Audit;
using Kavita.Models.Entities.User;
using Kavita.Models.Extensions;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Plus;

internal sealed class SeriesCollection
{
    public required IList<ExternalMetadataIdsDto> Series { get; set; }
    public required string Summary { get; set; }
    public required string Title { get; set; }
    /// <summary>
    /// Total items in the source, not what was matched
    /// </summary>
    public int TotalItems { get; set; }
}

public class SmartCollectionSyncService(
    IUnitOfWork unitOfWork,
    ILogger<SmartCollectionSyncService> logger,
    IEventHub eventHub,
    ILicenseService licenseService,
    IKavitaPlusAuditService auditService)
    : ISmartCollectionSyncService
{
    private const int SyncDelta = -2;
    // Allow 50 requests per 24 hours
    private static readonly RateLimiter RateLimiter = new RateLimiter(50, TimeSpan.FromHours(24), false);


    /// <summary>
    /// For every Sync-eligible collection, synchronize with upstream
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task Sync(CancellationToken ct = default)
    {
        if (!await licenseService.HasActiveLicense(ct: ct)) return;

        var expirationTime = DateTime.UtcNow.AddDays(SyncDelta).Truncate(TimeSpan.TicksPerHour);
        var collections = (await unitOfWork.CollectionTagRepository.GetAllCollectionsForSyncing(expirationTime, ct))
            .Where(CanSync)
            .ToList();

        logger.LogInformation("Found {Count} collections to synchronize", collections.Count);
        foreach (var collection in collections)
        {
            try
            {
                await SyncCollection(collection, ct);
            }
            catch (RateLimitException)
            {
                break;
            }
        }

        logger.LogInformation("Synchronization complete");
    }

    public async Task Sync(int collectionId, CancellationToken ct = default)
    {
        if (!await licenseService.HasActiveLicense(ct: ct)) return;

        var collection = await unitOfWork.CollectionTagRepository.GetCollectionAsync(collectionId, CollectionIncludes.Series, ct);
        if (!CanSync(collection))
        {
            logger.LogInformation("Requested to sync {CollectionName} but not applicable to sync", collection!.Title);
            return;
        }

        try
        {
            await SyncCollection(collection!, ct);
        } catch (RateLimitException) {/* Swallow */}
    }

    private static bool CanSync(AppUserCollection? collection)
    {
        if (collection is not {Source: ScrobbleProvider.Mal}) return false;
        if (string.IsNullOrEmpty(collection.SourceUrl)) return false;
        if (collection.LastSyncUtc.Truncate(TimeSpan.TicksPerHour) >= DateTime.UtcNow.AddDays(SyncDelta).Truncate(TimeSpan.TicksPerHour)) return false;
        return true;
    }

    private async Task SyncCollection(AppUserCollection collection, CancellationToken ct = default)
    {
        if (!RateLimiter.TryAcquire(string.Empty))
        {
            logger.LogDebug("Rate Limit hit for Smart Collection Sync");
            await auditService.LogAsync(
                KavitaPlusAuditCategory.Sync,
                KavitaPlusEventType.SyncFailed,
                AuditStatus.Failure,
                AuditSubjectType.Collection,
                subjectId: collection.Id,
                payload: new AuditLogCollectionFailedParamsDto { CollectionName = collection.Title },
                error: "rate-limit-hit",
                userId: collection.AppUserId,
                ct: ct);
            throw new RateLimitException();
        }

        var info = await GetStackInfo(GetStackId(collection.SourceUrl!));
        if (info == null)
        {
            logger.LogInformation("Unable to find collection through Kavita+");
            await auditService.LogAsync(
                KavitaPlusAuditCategory.Sync,
                KavitaPlusEventType.SyncFailed,
                AuditStatus.Failure,
                AuditSubjectType.Collection,
                subjectId: collection.Id,
                payload: new AuditLogCollectionFailedParamsDto { CollectionName = collection.Title },
                error: "api-unavailable",
                userId: collection.AppUserId,
                ct: ct);
            return;
        }

        await auditService.LogAsync(
            KavitaPlusAuditCategory.Sync,
            KavitaPlusEventType.SyncStarted,
            AuditStatus.Info,
            AuditSubjectType.Collection,
            subjectId: collection.Id,
            payload: new AuditLogCollectionStartedParamsDto { CollectionName = info.Title, StackId = collection.SourceUrl, TotalItems = info.TotalItems },
            userId: collection.AppUserId,
            ct: ct);

        // Check each series in the collection against what's in the target
        // For everything that's not there, link it up for this user.
        logger.LogInformation("Starting Sync on {CollectionName} with {SeriesCount} Series", info.Title, info.TotalItems);

        await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.SmartCollectionProgressEvent(info.Title, string.Empty, 0, info.TotalItems, ProgressEventType.Started), ct: ct);

        var missingCount = 0;
        var missingSeries = new StringBuilder();
        var counter = -1;
        foreach (var seriesInfo in info.Series.OrderBy(s => s.SeriesName))
        {
            counter++;
            try
            {
                // Normalize series name and localized name
                var normalizedSeriesName = seriesInfo.SeriesName?.ToNormalized();
                var normalizedLocalizedSeriesName = seriesInfo.LocalizedSeriesName?.ToNormalized();

                // Search for existing series in the collection
                var formats = seriesInfo.PlusMediaFormat.GetMangaFormats();
                var existingSeries = collection.Items.FirstOrDefault(s =>
                    (s.Name.ToNormalized() == normalizedSeriesName ||
                     s.NormalizedName == normalizedSeriesName ||
                     s.LocalizedName.ToNormalized() == normalizedLocalizedSeriesName ||
                     s.NormalizedLocalizedName == normalizedLocalizedSeriesName ||

                     s.NormalizedName == normalizedLocalizedSeriesName ||
                     s.NormalizedLocalizedName == normalizedSeriesName)
                    && formats.Contains(s.Format));

                logger.LogDebug("Trying to find {SeriesName} with formats ({Formats}) within Kavita for linking. Found: {ExistingSeriesName} ({ExistingSeriesId})",
                    seriesInfo.SeriesName, formats, existingSeries?.Name, existingSeries?.Id);

                if (existingSeries != null)
                {
                    await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                        MessageFactory.SmartCollectionProgressEvent(info.Title, seriesInfo.SeriesName, counter, info.TotalItems, ProgressEventType.Updated), ct: ct);
                    continue;
                }

                // Series not found in the collection, try to find it in the server
                var newSeries = await unitOfWork.SeriesRepository.GetSeriesByAnyNameAsync(seriesInfo.SeriesName,
                    seriesInfo.LocalizedSeriesName,
                    formats, collection.AppUserId, ct: ct);

                collection.Items ??= new List<Series>();
                if (newSeries != null)
                {
                    // Add the new series to the collection
                    collection.Items.Add(newSeries);
                    await auditService.LogCollectionAsync(KavitaPlusEventType.CollectionItemAdded, collection.Id,
                        new AuditLogCollectionItemParamsDto { CollectionName = collection.Title, SeriesName = newSeries.Name, SeriesId = newSeries.Id, Url = collection.SourceUrl }, userId: collection.AppUserId, ct: ct);
                }
                else
                {
                    logger.LogDebug("{Series} not found in the server", seriesInfo.SeriesName);
                    missingCount++;
                    missingSeries.Append(
                        $"<a href='{ScrobblingService.MalWeblinkWebsite}{seriesInfo.MalId}' target='_blank' rel='noopener noreferrer'>{seriesInfo.SeriesName}</a>");
                    missingSeries.Append("<br/>");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An exception occured when linking up a series to the collection. Skipping");
                missingCount++;
                missingSeries.Append(
                    $"<a href='{ScrobblingService.MalWeblinkWebsite}{seriesInfo.MalId}' target='_blank' rel='noopener noreferrer'>{seriesInfo.SeriesName}</a>");
                missingSeries.Append("<br/>");
            }

            await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.SmartCollectionProgressEvent(info.Title, seriesInfo.SeriesName, counter, info.TotalItems, ProgressEventType.Updated), ct: ct);
        }

        // At this point, all series in the info have been checked and added if necessary
        collection.LastSyncUtc = DateTime.UtcNow.Truncate(TimeSpan.TicksPerHour);
        collection.TotalSourceCount = info.TotalItems;
        collection.Summary = info.Summary;
        collection.MissingSeriesFromSource = missingSeries.ToString();

        unitOfWork.CollectionTagRepository.Update(collection);

        try
        {
            await unitOfWork.CommitAsync(ct);

            await unitOfWork.CollectionTagRepository.UpdateCollectionAgeRating(collection, ct);

            await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.SmartCollectionProgressEvent(info.Title, string.Empty, info.TotalItems, info.TotalItems, ProgressEventType.Ended), ct: ct);

            await eventHub.SendMessageAsync(MessageFactory.CollectionUpdated,
                MessageFactory.CollectionUpdatedEvent(collection.Id), false, ct);

            logger.LogInformation("Finished Syncing Collection {CollectionName} - Missing {MissingCount} series",
                collection.Title, missingCount);
            await auditService.LogCollectionAsync(KavitaPlusEventType.CollectionSynced, collection.Id,
                new AuditLogCollectionSyncedParamsDto { CollectionName = collection.Title, StackId = collection.SourceUrl,
                    ItemCount = collection.TotalSourceCount, MissingCount = missingCount, Url = collection.SourceUrl }, userId: collection.AppUserId, ct: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an error during saving the collection");
            await auditService.LogAsync(
                KavitaPlusAuditCategory.Sync,
                KavitaPlusEventType.SyncFailed,
                AuditStatus.Failure,
                AuditSubjectType.Collection,
                subjectId: collection.Id,
                payload: new AuditLogCollectionFailedParamsDto { CollectionName = collection.Title },
                error: ex.Message,
                userId: collection.AppUserId,
                ct: ct);
        }
    }



    private static long GetStackId(string url)
    {
        var tokens = url.Split("/");
        return long.Parse(tokens[^1], CultureInfo.InvariantCulture);
    }

    private async Task<SeriesCollection?> GetStackInfo(long stackId)
    {
        logger.LogDebug("Fetching Kavita+ for MAL Stack");

        var license = (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey)).Value;

        var seriesForStack = await ($"{Configuration.KavitaPlusApiUrl}/api/metadata/v2/stack?stackId=" + stackId)
            .WithKavitaPlusHeaders(license)
            .GetJsonAsync<SeriesCollection>();

        return seriesForStack;
    }
}
