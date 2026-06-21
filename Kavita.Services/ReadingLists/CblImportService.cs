using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.ReadingLists;
using Kavita.API.Services.SignalR;
using Kavita.Common.Extensions;
using Kavita.Models.Builders;
using Kavita.Models.DTOs.ReadingLists.CBL;
using Kavita.Models.DTOs.ReadingLists.CBL.Import;
using Kavita.Models.DTOs.ReadingLists.CBL.Internal;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.ReadingLists;
using Kavita.Services.Helpers;
using Flurl.Http;
using Kavita.Common;
using Kavita.Common.Helpers;
using Kavita.Models.Entities.Enums.ReadingList;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.ReadingLists;

public class CblImportService(IUnitOfWork unitOfWork, ICblGithubService cblGithubService, IEventHub eventHub,
    IDirectoryService directoryService, IReadingListService readingListService, IUrlValidationService urlValidationService,
    IImageService imageService, ILogger<CblImportService> logger) : ICblImportService
{
    public async Task<CblImportSummaryDto> ValidateList(int userId, string filePath)
    {
        ParsedCblReadingList cbl;
        try
        {
            cbl = CblParser.Parse(filePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse CBL file: {FilePath}", filePath.Sanitize());
            return new CblImportSummaryDto
            {
                CblName = string.Empty,
                FileName = Path.GetFileName(filePath),
                Success = CblImportResult.Fail,
                Results = [new CblBookResult { Reason = CblImportReason.InvalidFile }],
                SuccessfulInserts = []
            };
        }

        if (cbl.Items.Count == 0)
        {
            return new CblImportSummaryDto
            {
                CblName = cbl.Name,
                FileName = Path.GetFileName(filePath),
                Success = CblImportResult.Fail,
                Results = [new CblBookResult { Reason = CblImportReason.EmptyFile }],
                SuccessfulInserts = []
            };
        }

        var matchResults = await RunMatchingPipeline(userId, cbl);
        var summary = BuildSummary(cbl, filePath, matchResults);

        var existingList = await unitOfWork.ReadingListRepository
            .GetReadingListByTitleAsync(cbl.Name, userId);

        // Users may rename the underlying list causing a title lookup to fail, we fall back to lookup with the filename
        var sourcePathStem = new FileInfo(filePath).Name;
        existingList ??= await unitOfWork.ReadingListRepository.GetReadingListBySourcePathStemAsync(sourcePathStem, userId);

        summary.IsUpdate = existingList != null;

        return summary;
    }

    public async Task<CblImportSummaryDto> UpsertReadingList(int userId, string filePath, CblImportDecisions decisions, bool promote = false)
    {
        ParsedCblReadingList cbl;
        try
        {
            cbl = CblParser.Parse(filePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse CBL file: {FilePath}", filePath.Sanitize());
            return new CblImportSummaryDto
            {
                CblName = string.Empty,
                FileName = Path.GetFileName(filePath),
                Success = CblImportResult.Fail,
                Results = [new CblBookResult { Reason = CblImportReason.InvalidFile }],
                SuccessfulInserts = []
            };
        }

        if (cbl.Items.Count == 0)
        {
            return new CblImportSummaryDto
            {
                CblName = cbl.Name,
                FileName = Path.GetFileName(filePath),
                Success = CblImportResult.Fail,
                Results = [new CblBookResult { Reason = CblImportReason.EmptyFile }],
                SuccessfulInserts = []
            };
        }

        var matchResults = await RunMatchingPipeline(userId, cbl);

        // Override with user decisions
        foreach (var (order, decision) in decisions.ItemResolutions)
        {
            if (!matchResults.ContainsKey(order)) continue;

            var item = cbl.Items.FirstOrDefault(i => i.Order == order);
            if (item != null)
            {
                matchResults[order] = (
                    new MatchedItem(decision.SeriesId, decision.VolumeId, decision.ChapterId, CblMatchTier.UserDecision),
                    new CblBookResult(item)
                    {
                        Reason = CblImportReason.Success,
                        MatchTier = CblMatchTier.UserDecision,
                        SeriesId = decision.SeriesId,
                        ChapterId = decision.ChapterId
                    }
                );
            }
        }

        // Find or create reading list
        var readingList = await unitOfWork.ReadingListRepository
            .GetReadingListByTitleAsync(cbl.Name, userId, ReadingListIncludes.Tags | ReadingListIncludes.Items);
        var isUpdate = readingList != null;

        if (readingList == null)
        {
            readingList = new ReadingListBuilder(cbl.Name)
                .WithSummary(cbl.Summary ?? string.Empty)
                .WithAppUserId(userId)
                .WithPromoted(promote)
                .Build();

            unitOfWork.ReadingListRepository.Add(readingList);
        }



        // Set metadata from CBL
        await SetMetadataFromParsedCblAsync(cbl, readingList);

        // Add resolved items
        foreach (var (order, (match, _)) in matchResults.OrderBy(kv => kv.Key))
        {
            if (match == null) continue;
            ExistsOrAddReadingListItem(readingList, match.SeriesId, match.VolumeId, match.ChapterId, order);
        }

        // Save remap rules from user decisions
        if (decisions.SaveAsRemapRules && decisions.ItemResolutions.Count > 0)
        {
            foreach (var (order, decision) in decisions.ItemResolutions)
            {
                var item = cbl.Items.FirstOrDefault(i => i.Order == order);
                if (item == null) continue;

                var series = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(decision.SeriesId);

                unitOfWork.RemapRuleRepository.Add(new ReadingListRemapRule
                {
                    NormalizedCblSeriesName = item.SeriesName.ToNormalized(),
                    CblVolume = !string.IsNullOrEmpty(item.Volume) ? item.Volume : null,
                    CblNumber = !string.IsNullOrEmpty(item.Number) ? item.Number : null,
                    SeriesId = decision.SeriesId,
                    VolumeId = decision.VolumeId > 0 ? decision.VolumeId : null,
                    ChapterId = decision.ChapterId > 0 ? decision.ChapterId : null,
                    SeriesNameAtMapping = series?.Name ?? string.Empty,
                    AppUserId = userId,
                    IsGlobal = false,
                    CreatedUtc = DateTime.UtcNow
                });
            }
        }

        await unitOfWork.CommitAsync();

        // Generate cover image after commit so the reading list has an ID
        await GenerateCoverForReadingList(readingList, cbl.CoverImageUrls);

        await unitOfWork.CommitAsync();

        var summary = BuildSummary(cbl, filePath, matchResults);
        summary.IsUpdate = isUpdate;
        summary.ReadingListId = readingList.Id;

        return summary;
    }

    private async Task SetMetadataFromParsedCblAsync(ParsedCblReadingList cbl, ReadingList readingList)
    {
        readingList.TotalItemsAtImport = cbl.Items.Count;

        if (!string.IsNullOrEmpty(cbl.Summary))
        {
            readingList.Summary = cbl.Summary;
        }

        if (cbl.StartYear > 0)
        {
            readingList.StartingYear = cbl.StartYear;
        }

        if (cbl.StartMonth > 0)
        {
            readingList.StartingMonth = cbl.StartMonth;
        }

        if (cbl.EndYear > 0)
        {
            readingList.EndingYear = cbl.EndYear;
        }

        if (cbl.EndMonth > 0)
        {
            readingList.EndingMonth = cbl.EndMonth;
        }

        if (cbl.Tags.Count > 0)
        {
            await TagHelper.UpdateEntityTags(readingList.Tags, cbl.Tags, unitOfWork.DataContext.ReadingListTag, unitOfWork, false);
        }
    }

    /// <summary>
    /// Attempts to set a cover image on the reading list if not already locked/set.
    /// First tries the v2 cover URL, then falls back to merged cover generation.
    /// </summary>
    /// <remarks>Does not commit changes</remarks>
    private async Task GenerateCoverForReadingList(ReadingList readingList, IList<string> coverImageUrls)
    {
        if (readingList.CoverImageLocked || !string.IsNullOrEmpty(readingList.CoverImage)) return;

        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();

        // Try v2 cover URL first
        var coverUrl = coverImageUrls.FirstOrDefault();
        if (!string.IsNullOrEmpty(coverUrl))
        {
            try
            {
                await urlValidationService.ValidateUrlAsync(coverUrl);
                var (width, height) = settings.CoverImageSize.GetDimensions();
                var fileName = await imageService.CreateThumbnailFromUrl(
                    coverUrl,
                    ImageService.GetReadingListFormat(readingList.Id),
                    settings.EncodeMediaAs,
                    width,
                    height);

                if (!string.IsNullOrEmpty(fileName))
                {
                    readingList.CoverImage = fileName;
                    imageService.UpdateColorScape(readingList);
                    return;
                }
            }
            catch (KavitaException ex)
            {
                logger.LogError(ex, "Cover URL for {ReadingListTitle} ({ReadingListId}) is not secure or valid, falling back to default generation",
                    readingList.Title, readingList.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to download cover from URL for {ReadingListTitle} ({ReadingListId}), falling back to default generation",
                    readingList.Title, readingList.Id);
            }
        }

        // Fallback to merged cover generation
        await readingListService.GenerateReadingListCoverImage(readingList);
    }

    public async Task SyncReadingListAsync(int userId, int readingListId, bool force = false)
    {
        var readingList = await unitOfWork.ReadingListRepository
            .GetReadingListByIdAsync(readingListId, ReadingListIncludes.Tags | ReadingListIncludes.Items);

        if (readingList is not {CanSync: true} || readingList.AppUserId != userId)
        {
            logger.LogWarning("Cannot sync reading list: {ReadingListId}. List is either not found, not syncable, or wrong user", readingListId);
            return;
        }

        string content;
        string? contentHash;

        if (!string.IsNullOrEmpty(readingList.SourcePath)) // Github-based list
        {
            try
            {
                var remoteSha = await cblGithubService.GetFileSha(readingList.SourcePath);
                if (await CheckAndMarkIfNoChanges(readingList, remoteSha, force))
                {
                    return;
                }

                contentHash = remoteSha;
                content = await cblGithubService.GetFileContent(readingList.SourcePath);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to download CBL content for sync: {SourcePath}", readingList.SourcePath);
                readingList.LastSyncCheckUtc = DateTime.UtcNow;
                await unitOfWork.CommitAsync();
                return;
            }
        }
        else if (!string.IsNullOrEmpty(readingList.DownloadUrl)) // Url-based list
        {
            try
            {
                await urlValidationService.ValidateUrlAsync(readingList.DownloadUrl);
                content = await FlurlConfiguration.CreateSafeRequest(readingList.DownloadUrl).GetStringAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to download CBL content for sync from URL: {Url}", readingList.DownloadUrl);
                readingList.LastSyncCheckUtc = DateTime.UtcNow;
                await unitOfWork.CommitAsync();
                return;
            }

            contentHash = FileService.ComputeSha256(content);
            if (await CheckAndMarkIfNoChanges(readingList, contentHash, force))
            {
                return;
            }
        }
        else
        {
            logger.LogWarning("Reading list {ReadingListId} is marked syncable but has no SourcePath or DownloadUrl", readingListId);
            return;
        }

        // Save to temp file for parsing
        var tempDir = Path.Join(directoryService.TempDirectory, $"{userId}", "cbl-sync");
        directoryService.ExistOrCreate(tempDir);

        var sourceRef = readingList.SourcePath ?? readingList.DownloadUrl ?? $"list-{readingListId}";
        var tempFile = Path.Join(tempDir, $"sync-{readingListId}{GetExtension(sourceRef)}");

        await directoryService.FileSystem.File.WriteAllTextAsync(tempFile, content);

        try
        {
            var cbl = CblParser.Parse(tempFile);
            if (cbl.Items.Count == 0)
            {
                logger.LogWarning("Syncing of Reading List {ReadingListTitle} ({ReadingListId}) has 0 items from parsing, aborting", readingList.Title, readingList.Id);
                return;
            }

            var matchResults = await RunMatchingPipeline(userId, cbl);

            // Validate there are at least results in case a bad file
            if (matchResults.Count == 0)
            {
                logger.LogWarning("Syncing of Reading List {ReadingListTitle} ({ReadingListId}) has 0 matches, aborting", readingList.Title, readingList.Id);
                return;
            }

            // Clear existing items and re-add
            readingList.Items.Clear();

            foreach (var (order, (match, _)) in matchResults.OrderBy(kv => kv.Key))
            {
                if (match == null) continue;
                ExistsOrAddReadingListItem(readingList, match.SeriesId, match.VolumeId, match.ChapterId, order);
            }

            // Update metadata
            await SetMetadataFromParsedCblAsync(cbl, readingList);

            if (!string.IsNullOrEmpty(contentHash))
            {
                readingList.ShaHash = contentHash; // This is either Github Sha hash or the content hash
            }
            readingList.LastSyncedUtc = DateTime.UtcNow;
            readingList.LastSyncCheckUtc = DateTime.UtcNow;

            await unitOfWork.CommitAsync();

            // Re-run side effects like age ratings, cover generation, etc
            await readingListService.CalculateReadingListAgeRating(readingList);

            // Don't calculate from issue-level metadata if already set from json file
            if (ShouldCalcReleaseDatesFromIssues(readingList))
            {
                await readingListService.CalculateStartAndEndDates(readingList);
            }

            await GenerateCoverForReadingList(readingList, cbl.CoverImageUrls);

            await unitOfWork.CommitAsync();

            // Inform the UI that the CBL was updated
            await eventHub.SendMessageAsync(MessageFactory.ReadingListUpdated,
                MessageFactory.ReadingListUpdatedEvent(readingListId), false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync reading list {ReadingListId} from {Source}", readingListId, readingList.SourcePath ?? readingList.DownloadUrl);
        }
        finally
        {
            try { directoryService.FileSystem.File.Delete(tempFile); } catch { /* The file will be cleaned up with nightly, okay to swallow */ }
        }
    }

    public static bool ShouldCalcReleaseDatesFromIssues(ReadingList readingList)
    {
        var url = readingList.SourcePath ??  readingList.DownloadUrl;
        var isV2 = readingList.Provider == ReadingListProvider.Url && !string.IsNullOrEmpty(url) && url.EndsWith(".json");

        if (!isV2) return true;

        // v2 lists don't have Months for some reason
        var hasStartDate = readingList is { StartingYear: > 0 };
        var hasEndDate = readingList is { EndingYear: > 0 };

        return isV2 && !hasStartDate && !hasEndDate;
    }

    private async Task<bool> CheckAndMarkIfNoChanges(ReadingList readingList, string hash, bool force)
    {
        if (force || readingList.HasRemoteChange(hash)) return false;

        logger.LogDebug("Sync of Reading List {ReadingListTitle} ({ReadingListId}) has no remote changes", readingList.Title, readingList.Id);
        readingList.LastSyncCheckUtc = DateTime.UtcNow;
        await unitOfWork.CommitAsync();
        return true;

    }

    /// <summary>
    /// For every user with syncable cbl reading lists that haven't been checked within the last 3 days (LastSyncCheckUtc), attempt to update them.
    /// </summary>
    /// <remarks>Failures to match will be logged, but will not inhibit the process</remarks>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task SyncAllReadingLists(CancellationToken cancellationToken = default)
    {
        var syncThreshold = DateTime.UtcNow.AddDays(-3);
        var syncableMap = await unitOfWork.ReadingListRepository
            .GetSyncableReadingListsAsync(syncThreshold, cancellationToken);

        if (syncableMap.Count == 0)
        {
            logger.LogInformation("CBL Sync: No reading lists due for sync");
            return;
        }

        var totalSynced = 0;
        var totalFailed = 0;

        foreach (var (userId, readingListIds) in syncableMap)
        {
            foreach (var readingListId in readingListIds)
            {
                if (cancellationToken.IsCancellationRequested) return;

                try
                {
                    await SyncReadingListAsync(userId, readingListId);
                    totalSynced++;
                }
                catch (Exception ex)
                {
                    totalFailed++;
                    logger.LogError(ex, "Failed to sync reading list {ReadingListId} for user {UserId}", readingListId, userId);
                }
            }
        }

        logger.LogInformation("CBL Sync complete: {Synced} synced, {Failed} failed", totalSynced, totalFailed);
    }

    private async Task<Dictionary<int, (MatchedItem? Match, CblBookResult Result)>> RunMatchingPipeline(
        int userId, ParsedCblReadingList cbl)
    {
        // Collect all unique normalized names + variants
        var nameVariants = CblSeriesMatcher.GenerateAllNameVariants(cbl.Items);
        var allNormalizedNames = nameVariants.Keys.ToList();

        // Also include direct normalized names for remap rule lookup
        var directNormalizedNames = cbl.Items
            .Select(i => i.SeriesName.ToNormalized())
            .Distinct()
            .ToList();


        allNormalizedNames.AddRange(directNormalizedNames.Where(n => !allNormalizedNames.Contains(n)));

        // Collect external IDs
        var kavitaIds = cbl.Items
            .SelectMany(i => i.ExternalIds)
            .Where(e => e.Provider == CblExternalDbProvider.Kavita && int.TryParse(e.IssueId, out _))
            .Select(e => int.Parse(e.IssueId))
            .Distinct()
            .ToList();

        var comicVineIds = cbl.Items
            .SelectMany(i => i.ExternalIds)
            .Where(e => e.Provider == CblExternalDbProvider.ComicVine && !string.IsNullOrEmpty(e.IssueId))
            .Select(e => e.IssueId)
            .Distinct()
            .ToList();

        var metronIds = cbl.Items
            .SelectMany(i => i.ExternalIds)
            .Where(e => e.Provider == CblExternalDbProvider.Metron && long.TryParse(e.IssueId, out _))
            .Select(e => long.Parse(e.IssueId))
            .Distinct()
            .ToList();

        // Get user's accessible library IDs
        var userLibraryIds = await unitOfWork.LibraryRepository.GetLibraryIdsForUserIdAsync(userId);

        // Batch DB queries
        var remapRules = await unitOfWork.RemapRuleRepository
            .GetRulesForNamesAsync(directNormalizedNames, userId);

        var externalIdChapters = await unitOfWork.ChapterRepository
            .GetChaptersByExternalIdsAsync(kavitaIds, comicVineIds, metronIds, userLibraryIds);

        var matchedSeries = (await unitOfWork.SeriesRepository
            .GetAllSeriesByNameAsync(allNormalizedNames, userId,
                SeriesIncludes.Chapters | SeriesIncludes.Metadata)).ToList();

        // Also fetch series referenced by remap rules that weren't caught by name matching
        var remapSeriesIds = remapRules
            .Where(r => !r.ChapterId.HasValue || !r.VolumeId.HasValue)
            .Select(r => r.SeriesId)
            .Where(id => matchedSeries.All(s => s.Id != id))
            .Distinct()
            .ToList();

        if (remapSeriesIds.Count > 0)
        {
            var remapSeries = await unitOfWork.SeriesRepository
                .GetSeriesByIdsAsync(remapSeriesIds);
            matchedSeries.AddRange(remapSeries);
        }

        // We'll run AlternateSeries for all names, the matcher will only use it as fallback
        var alternateSeriesChapters = await unitOfWork.ChapterRepository
            .GetChaptersByAlternateSeriesAsync(directNormalizedNames, userLibraryIds);

        return CblSeriesMatcher.ResolveAll(cbl.Items, remapRules, externalIdChapters,
            matchedSeries, alternateSeriesChapters);
    }

    private static CblImportSummaryDto BuildSummary(ParsedCblReadingList cbl, string filePath,
        Dictionary<int, (MatchedItem? Match, CblBookResult Result)> matchResults)
    {
        var results = new List<CblBookResult>();
        var successfulInserts = new List<CblBookResult>();

        foreach (var (_, (match, result)) in matchResults.OrderBy(kv => kv.Key))
        {
            if (match != null && result.Reason == CblImportReason.Success)
            {
                successfulInserts.Add(result);
            }
            else
            {
                results.Add(result);
            }
        }

        var success = CblImportResult.Success;
        if (successfulInserts.Count == 0 && results.Count > 0)
        {
            success = CblImportResult.Fail;
        }
        else if (results.Count > 0)
        {
            success = CblImportResult.Partial;
        }

        return new CblImportSummaryDto
        {
            CblName = cbl.Name,
            FileName = Path.GetFileName(filePath),
            Success = success,
            Results = results,
            SuccessfulInserts = successfulInserts
        };
    }

    private static void ExistsOrAddReadingListItem(ReadingList readingList, int seriesId, int volumeId, int chapterId, int order)
    {
        var existing = readingList.Items.FirstOrDefault(item =>
            item.SeriesId == seriesId && item.ChapterId == chapterId);
        if (existing != null)
        {
            existing.Order = order;
            return;
        }

        var newItem = new ReadingListItemBuilder(order, seriesId, volumeId, chapterId).Build();
        readingList.Items.Add(newItem);
    }

    private static string GetExtension(string path)
    {
        var ext = Path.GetExtension(path);
        return string.IsNullOrEmpty(ext) ? ".cbl" : ext;
    }
}
