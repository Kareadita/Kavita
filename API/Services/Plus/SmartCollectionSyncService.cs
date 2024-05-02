using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Recommendation;
using API.DTOs.Scrobbling;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using Flurl.Http;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Microsoft.Extensions.Logging;

namespace API.Services.Plus;
#nullable enable

sealed class SeriesCollection
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
    private const int SyncDelta = -2;
    // Allow 50 requests per 24 hours
    private static readonly RateLimiter RateLimiter = new RateLimiter(50, TimeSpan.FromHours(24), false);


    public SmartCollectionSyncService(IUnitOfWork unitOfWork, ILogger<SmartCollectionSyncService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// For every Sync-eligible collection, syncronize with upstream
    /// </summary>
    /// <returns></returns>
    public async Task Sync()
    {
        var collections = await _unitOfWork.CollectionTagRepository.GetAllCollectionsForSyncing(DateTime.UtcNow.AddDays(SyncDelta));
        foreach (var collection in collections.Where(CanSync))
        {
            await SyncCollection(collection);
        }
    }

    public async Task Sync(int collectionId)
    {
        var collection = await _unitOfWork.CollectionTagRepository.GetCollectionAsync(collectionId, CollectionIncludes.Series);
        if (!CanSync(collection)) return;
        await SyncCollection(collection!);
    }

    private static bool CanSync(AppUserCollection? collection)
    {
        if (collection is not {Source: ScrobbleProvider.Mal}) return false;
        if (string.IsNullOrEmpty(collection.SourceUrl)) return false;
        if (collection.LastSyncUtc >= DateTime.UtcNow.AddDays(SyncDelta)) return false;
        return true;
    }

    private async Task SyncCollection(AppUserCollection collection)
    {
        if (!RateLimiter.TryAcquire(string.Empty))
        {
            // Request not allowed due to rate limit
            _logger.LogDebug("Rate Limit hit for Smart Collection Sync");
            return;
        }

        var info = await GetStackInfo(GetStackId(collection.SourceUrl!));

        if (info == null) return;

        // Check each series in the collection against what's in the target
        // For everything that's not there, link it up for this user.

        // Check each series in the collection against what's in the target
        // For everything that's not there, link it up for this user.
        _logger.LogInformation("Adding new series to collection");

        var missingCount = 0;
        foreach (var seriesInfo in info.Series)
        {
            // Normalize series name and localized name
            var normalizedSeriesName = seriesInfo.SeriesName?.ToNormalized();
            var normalizedLocalizedSeriesName = seriesInfo.LocalizedSeriesName?.ToNormalized();

            // Search for existing series in the collection
            var existingSeries = collection.Items.FirstOrDefault(s =>
                s.Name.ToNormalized() == normalizedSeriesName ||
                s.NormalizedName == normalizedSeriesName ||
                s.LocalizedName.ToNormalized() == normalizedLocalizedSeriesName ||
                s.NormalizedLocalizedName == normalizedLocalizedSeriesName);

            if (existingSeries == null)
            {
                // Series not found in the collection, try to find it in the server
                var newSeries = await _unitOfWork.SeriesRepository.GetSeriesByAnyName(seriesInfo.SeriesName, seriesInfo.LocalizedSeriesName,
                    GetMangaFormats(seriesInfo.PlusMediaFormat), collection.AppUserId);

                if (newSeries != null)
                {
                    // Add the new series to the collection
                    collection.Items.Add(newSeries);
                }
                else
                {
                    _logger.LogWarning("{Series} not found in the server", seriesInfo.SeriesName);
                    missingCount++;
                }
            }
        }

        // At this point, all series in the info have been checked and added if necessary
        // You may want to commit changes to the database if needed
        collection.LastSyncUtc = DateTime.UtcNow;
        collection.TotalSourceCount = info.TotalItems;
        collection.Summary = info.Summary;
        _unitOfWork.CollectionTagRepository.Update(collection);

        await _unitOfWork.CommitAsync();

        _logger.LogInformation("Finished Syncing Collection {CollectionName} - Missing {MissingCount} series", collection.Title, missingCount);
    }

    private static IList<MangaFormat> GetMangaFormats(MediaFormat? mediaFormat)
    {
        if (mediaFormat == null) return [MangaFormat.Archive];
        return mediaFormat switch
        {
            MediaFormat.Manga => [MangaFormat.Archive, MangaFormat.Image],
            MediaFormat.Comic => [MangaFormat.Archive],
            MediaFormat.LightNovel => [MangaFormat.Epub, MangaFormat.Pdf],
            MediaFormat.Book => [MangaFormat.Epub, MangaFormat.Pdf],
            MediaFormat.Unknown => [MangaFormat.Archive],
            _ => [MangaFormat.Archive]
        };
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
            .WithHeader("Accept", "application/json")
            .WithHeader("User-Agent", "Kavita")
            .WithHeader("x-license-key", license)
            .WithHeader("x-installId", HashUtil.ServerToken())
            .WithHeader("x-kavita-version", BuildInfo.Version)
            .WithHeader("Content-Type", "application/json")
            .WithTimeout(TimeSpan.FromSeconds(Configuration.DefaultTimeOutSecs))
            .GetJsonAsync<SeriesCollection>();

        return seriesForStack;
    }
}
