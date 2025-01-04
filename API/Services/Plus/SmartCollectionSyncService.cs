using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.KavitaPlus.ExternalMetadata;
using API.DTOs.Scrobbling;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using API.SignalR;
using Flurl.Http;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Microsoft.Extensions.Logging;

namespace API.Services.Plus;
#nullable enable

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
/// Responsible to synchronize Collection series from non-Kavita sources
/// </summary>
public interface ISmartCollectionSyncService
{
    /// <summary>
    /// Synchronize all collections
    /// </summary>
    /// <returns></returns>
    Task Sync();
    /// <summary>
    /// Synchronize a collection
    /// </summary>
    /// <param name="collectionId"></param>
    /// <returns></returns>
    Task Sync(int collectionId);
}

public class SmartCollectionSyncService : ISmartCollectionSyncService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SmartCollectionSyncService> _logger;
    private readonly IEventHub _eventHub;
    private readonly ILicenseService _licenseService;

    private const int SyncDelta = -2;
    // Allow 50 requests per 24 hours
    private static readonly RateLimiter RateLimiter = new RateLimiter(50, TimeSpan.FromHours(24), false);


    public SmartCollectionSyncService(IUnitOfWork unitOfWork, ILogger<SmartCollectionSyncService> logger,
        IEventHub eventHub, ILicenseService licenseService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _eventHub = eventHub;
        _licenseService = licenseService;
    }

    /// <summary>
    /// For every Sync-eligible collection, synchronize with upstream
    /// </summary>
    /// <returns></returns>
    public async Task Sync()
    {
        if (!await _licenseService.HasActiveLicense()) return;
        var expirationTime = DateTime.UtcNow.AddDays(SyncDelta).Truncate(TimeSpan.TicksPerHour);
        var collections = (await _unitOfWork.CollectionTagRepository.GetAllCollectionsForSyncing(expirationTime))
            .Where(CanSync)
            .ToList();

        _logger.LogInformation("Found {Count} collections to synchronize", collections.Count);
        foreach (var collection in collections)
        {
            try
            {
                await SyncCollection(collection);
            }
            catch (RateLimitException)
            {
                break;
            }
        }

        _logger.LogInformation("Synchronization complete");
    }

    public async Task Sync(int collectionId)
    {
        if (!await _licenseService.HasActiveLicense()) return;
        var collection = await _unitOfWork.CollectionTagRepository.GetCollectionAsync(collectionId, CollectionIncludes.Series);
        if (!CanSync(collection))
        {
            _logger.LogInformation("Requested to sync {CollectionName} but not applicable to sync", collection!.Title);
            return;
        }

        try
        {
            await SyncCollection(collection!);
        } catch (RateLimitException) {/* Swallow */}
    }

    private static bool CanSync(AppUserCollection? collection)
    {
        if (collection is not {Source: ScrobbleProvider.Mal}) return false;
        if (string.IsNullOrEmpty(collection.SourceUrl)) return false;
        if (collection.LastSyncUtc.Truncate(TimeSpan.TicksPerHour) >= DateTime.UtcNow.AddDays(SyncDelta).Truncate(TimeSpan.TicksPerHour)) return false;
        return true;
    }

    private async Task SyncCollection(AppUserCollection collection)
    {
        if (!RateLimiter.TryAcquire(string.Empty))
        {
            // Request not allowed due to rate limit
            _logger.LogDebug("Rate Limit hit for Smart Collection Sync");
            throw new RateLimitException();
        }

        var info = await GetStackInfo(GetStackId(collection.SourceUrl!));
        if (info == null)
        {
            _logger.LogInformation("Unable to find collection through Kavita+");
            return;
        }

        // Check each series in the collection against what's in the target
        // For everything that's not there, link it up for this user.
        _logger.LogInformation("Starting Sync on {CollectionName} with {SeriesCount} Series", info.Title, info.TotalItems);

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.SmartCollectionProgressEvent(info.Title, string.Empty, 0, info.TotalItems, ProgressEventType.Started));

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

                _logger.LogDebug("Trying to find {SeriesName} with formats ({Formats}) within Kavita for linking. Found: {ExistingSeriesName} ({ExistingSeriesId})",
                    seriesInfo.SeriesName, formats, existingSeries?.Name, existingSeries?.Id);

                if (existingSeries != null)
                {
                    await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                        MessageFactory.SmartCollectionProgressEvent(info.Title, seriesInfo.SeriesName, counter, info.TotalItems, ProgressEventType.Updated));
                    continue;
                }

                // Series not found in the collection, try to find it in the server
                var newSeries = await _unitOfWork.SeriesRepository.GetSeriesByAnyName(seriesInfo.SeriesName,
                    seriesInfo.LocalizedSeriesName,
                    formats, collection.AppUserId);

                collection.Items ??= new List<Series>();
                if (newSeries != null)
                {
                    // Add the new series to the collection
                    collection.Items.Add(newSeries);

                }
                else
                {
                    _logger.LogDebug("{Series} not found in the server", seriesInfo.SeriesName);
                    missingCount++;
                    missingSeries.Append(
                        $"<a href='{ScrobblingService.MalWeblinkWebsite}{seriesInfo.MalId}' target='_blank' rel='noopener noreferrer'>{seriesInfo.SeriesName}</a>");
                    missingSeries.Append("<br/>");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An exception occured when linking up a series to the collection. Skipping");
                missingCount++;
                missingSeries.Append(
                    $"<a href='{ScrobblingService.MalWeblinkWebsite}{seriesInfo.MalId}' target='_blank' rel='noopener noreferrer'>{seriesInfo.SeriesName}</a>");
                missingSeries.Append("<br/>");
            }

            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.SmartCollectionProgressEvent(info.Title, seriesInfo.SeriesName, counter, info.TotalItems, ProgressEventType.Updated));
        }

        // At this point, all series in the info have been checked and added if necessary
        // You may want to commit changes to the database if needed
        collection.LastSyncUtc = DateTime.UtcNow.Truncate(TimeSpan.TicksPerHour);
        collection.TotalSourceCount = info.TotalItems;
        collection.Summary = info.Summary;
        collection.MissingSeriesFromSource = missingSeries.ToString();

        _unitOfWork.CollectionTagRepository.Update(collection);

        try
        {
            await _unitOfWork.CommitAsync();

            await _unitOfWork.CollectionTagRepository.UpdateCollectionAgeRating(collection);

            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.SmartCollectionProgressEvent(info.Title, string.Empty, info.TotalItems, info.TotalItems, ProgressEventType.Ended));

            await _eventHub.SendMessageAsync(MessageFactory.CollectionUpdated,
                MessageFactory.CollectionUpdatedEvent(collection.Id), false);

            _logger.LogInformation("Finished Syncing Collection {CollectionName} - Missing {MissingCount} series",
                collection.Title, missingCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an error during saving the collection");
        }
    }



    private static long GetStackId(string url)
    {
        var tokens = url.Split("/");
        return long.Parse(tokens[^1], CultureInfo.InvariantCulture);
    }

    private async Task<SeriesCollection?> GetStackInfo(long stackId)
    {
        _logger.LogDebug("Fetching Kavita+ for MAL Stack");

        var license = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey)).Value;

        var seriesForStack = await ($"{Configuration.KavitaPlusApiUrl}/api/metadata/v2/stack?stackId=" + stackId)
            .WithKavitaPlusHeaders(license)
            .GetJsonAsync<SeriesCollection>();

        return seriesForStack;
    }
}
