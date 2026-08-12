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

/// <summary>
/// Outcome of linking an upstream stack's series to a collection
/// </summary>
internal sealed class CollectionLinkResult
{
    /// <summary>
    /// Number of source series that could not be found on the server
    /// </summary>
    public int MissingCount { get; set; }
    /// <summary>
    /// A &lt;br/&gt; separated string of anchor links for every missing series
    /// </summary>
    public string MissingSeries { get; set; } = string.Empty;
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

        var linkResult = await LinkSeriesToCollection(collection, info, ct);
        var missingCount = linkResult.MissingCount;

        // At this point, all series in the info have been checked and added if necessary
        collection.LastSyncUtc = DateTime.UtcNow.Truncate(TimeSpan.TicksPerHour);
        collection.TotalSourceCount = info.TotalItems;
        collection.Summary = info.Summary;
        collection.MissingSeriesFromSource = linkResult.MissingSeries;

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



    /// <summary>
    /// For each series in the upstream stack, find the matching Kavita series and link it to the collection if it
    /// isn't already present. Series that can't be found on the server are collected into the returned result.
    /// </summary>
    internal async Task<CollectionLinkResult> LinkSeriesToCollection(AppUserCollection collection, SeriesCollection info, CancellationToken ct = default)
    {
        var result = new CollectionLinkResult();
        var missingSeries = new StringBuilder();
        var counter = -1;

        collection.Items ??= new List<Series>();

        foreach (var seriesInfo in info.Series.OrderBy(s => s.SeriesName))
        {
            counter++;
            try
            {
                var match = await MatchSeries(collection, seriesInfo, ct);

                logger.LogDebug("Trying to find {SeriesName} ({Format}) within Kavita for linking. Found: {ExistingSeriesName} ({ExistingSeriesId})",
                    seriesInfo.SeriesName, seriesInfo.PlusMediaFormat, match?.Name, match?.Id);

                if (match == null)
                {
                    logger.LogDebug("{Series} not found in the server", seriesInfo.SeriesName);
                    result.MissingCount++;
                    AppendMissingSeries(missingSeries, seriesInfo);
                }
                else if (IsAlreadyInCollection(collection, match))
                {
                    logger.LogDebug("{SeriesName} already present in collection {CollectionName}", match.Name, collection.Title);
                }
                else
                {
                    collection.Items.Add(match);
                    await auditService.LogCollectionAsync(KavitaPlusEventType.CollectionItemAdded, collection.Id,
                        new AuditLogCollectionItemParamsDto { CollectionName = collection.Title, SeriesName = match.Name, SeriesId = match.Id, Url = collection.SourceUrl },
                        userId: collection.AppUserId, ct: ct);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An exception occured when linking up a series to the collection. Skipping");
                result.MissingCount++;
                AppendMissingSeries(missingSeries, seriesInfo);
            }

            await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.SmartCollectionProgressEvent(info.Title, seriesInfo.SeriesName, counter, info.TotalItems, ProgressEventType.Updated), ct: ct);
        }

        result.MissingSeries = missingSeries.ToString();
        return result;
    }

    /// <summary>
    /// Resolve the Kavita series that corresponds to an upstream stack entry. The <paramref name="seriesInfo"/> already
    /// carries the external ids, so id-priority matching is used first with a normalized name fallback.
    /// </summary>
    private async Task<Series?> MatchSeries(AppUserCollection collection, ExternalMetadataIdsDto seriesInfo, CancellationToken ct)
    {
        var formats = seriesInfo.PlusMediaFormat.GetMangaFormats();
        var names = new[] { seriesInfo.SeriesName, seriesInfo.LocalizedSeriesName }
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(s => s!)
            .ToList();

        return await unitOfWork.SeriesRepository.GetSeriesFromExternalMetadata(
            names, formats, collection.AppUserId, seriesInfo, SeriesIncludes.None, ct);
    }

    private static bool IsAlreadyInCollection(AppUserCollection collection, Series match)
    {
        return collection.Items.Any(s =>
            s.Id == match.Id
            || (s.Format == match.Format
                && (s.NormalizedName == match.NormalizedName
                    || s.NormalizedLocalizedName == match.NormalizedLocalizedName)));
    }

    private static void AppendMissingSeries(StringBuilder builder, ExternalMetadataIdsDto seriesInfo)
    {
        builder.Append(
            $"<a href='{ScrobblingService.MalWeblinkWebsite}{seriesInfo.MalId}' target='_blank' rel='noopener noreferrer'>{seriesInfo.SeriesName}</a>");
        builder.Append("<br/>");
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
