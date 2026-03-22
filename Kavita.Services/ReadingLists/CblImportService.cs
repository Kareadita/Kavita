using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.ReadingLists;
using Kavita.Common.Extensions;
using Kavita.Models.Builders;
using Kavita.Models.DTOs.ReadingLists.CBL;
using Kavita.Models.DTOs.ReadingLists.CBL.Import;
using Kavita.Models.DTOs.ReadingLists.CBL.Internal;
using Kavita.Models.Entities.ReadingLists;
using Kavita.Services.Helpers;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.ReadingLists;

public class CblImportService(IUnitOfWork unitOfWork, ICblGithubService cblGithubService,
    IDirectoryService directoryService, ILogger<CblImportService> logger) : ICblImportService
{
    public async Task<CblImportSummaryDto> ValidateList(int userId, string filePath, CblImportOptions options)
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

        var matchResults = await RunMatchingPipeline(userId, cbl, options);
        var summary = BuildSummary(cbl, filePath, matchResults);

        var existingList = await unitOfWork.ReadingListRepository
            .GetReadingListByTitleAsync(cbl.Name, userId);
        summary.IsUpdate = existingList != null;

        return summary;
    }

    public async Task<CblImportSummaryDto> UpsertReadingList(int userId, string filePath, CblImportOptions options, CblImportDecisions decisions)
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

        var matchResults = await RunMatchingPipeline(userId, cbl, options);

        // Override with user decisions
        foreach (var (order, decision) in decisions.ItemResolutions)
        {
            if (matchResults.ContainsKey(order))
            {
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
        }

        // Find or create reading list
        var readingList = await unitOfWork.ReadingListRepository
            .GetReadingListByTitleAsync(cbl.Name, userId);
        var isUpdate = readingList != null;

        if (readingList == null)
        {
            readingList = new ReadingListBuilder(cbl.Name)
                .WithSummary(cbl.Summary ?? string.Empty)
                .WithAppUserId(userId)
                .Build();

            unitOfWork.ReadingListRepository.Add(readingList);
        }

        // Set metadata from CBL
        if (!string.IsNullOrEmpty(cbl.Summary))
            readingList.Summary = cbl.Summary;
        if (cbl.StartYear > 0)
            readingList.StartingYear = cbl.StartYear;
        if (cbl.StartMonth > 0)
            readingList.StartingMonth = cbl.StartMonth;
        if (cbl.EndYear > 0)
            readingList.EndingYear = cbl.EndYear;
        if (cbl.EndMonth > 0)
            readingList.EndingMonth = cbl.EndMonth;

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

        var summary = BuildSummary(cbl, filePath, matchResults);
        summary.IsUpdate = isUpdate;
        return summary;
    }

    public async Task SyncReadingList(int userId, int readingListId)
    {
        var readingList = await unitOfWork.ReadingListRepository
            .GetReadingListByIdAsync(readingListId, ReadingListIncludes.Items);

        if (readingList is not {CanSync: true} || readingList.AppUserId != userId)
        {
            logger.LogWarning("Cannot sync reading list {ReadingListId} — not found, not syncable, or wrong user", readingListId);
            return;
        }

        // Re-download from GitHub
        string content;
        try
        {
            content = await cblGithubService.GetFileContent(readingList.SourcePath!);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download CBL content for sync: {SourcePath}", readingList.SourcePath);
            readingList.LastSyncCheckUtc = DateTime.UtcNow;
            await unitOfWork.CommitAsync();
            return;
        }

        // Save to temp file for parsing
        var tempDir = Path.Join(directoryService.TempDirectory, $"{userId}", "cbl-sync");
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Join(tempDir, $"sync-{readingListId}{GetExtension(readingList.SourcePath!)}");
        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            var cbl = CblParser.Parse(tempFile);
            if (cbl.Items.Count == 0) return;

            var options = new CblImportOptions();
            var matchResults = await RunMatchingPipeline(userId, cbl, options);

            // Clear existing items and re-add
            readingList.Items.Clear();

            foreach (var (order, (match, _)) in matchResults.OrderBy(kv => kv.Key))
            {
                if (match == null) continue;
                ExistsOrAddReadingListItem(readingList, match.SeriesId, match.VolumeId, match.ChapterId, order);
            }

            // Update metadata
            if (!string.IsNullOrEmpty(cbl.Summary))
                readingList.Summary = cbl.Summary;
            if (cbl.StartYear > 0)
                readingList.StartingYear = cbl.StartYear;
            if (cbl.EndYear > 0)
                readingList.EndingYear = cbl.EndYear;

            readingList.LastSyncedUtc = DateTime.UtcNow;
            readingList.LastSyncCheckUtc = DateTime.UtcNow;

            await unitOfWork.CommitAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync reading list {ReadingListId} from {SourcePath}", readingListId, readingList.SourcePath);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { /* best effort cleanup */ }
        }
    }

    private async Task<Dictionary<int, (MatchedItem? Match, CblBookResult Result)>> RunMatchingPipeline(
        int userId, ParsedCblReadingList cbl, CblImportOptions options)
    {
        // Collect all unique normalized names + variants
        var nameVariants = CblSeriesMatcher.GenerateAllNameVariants(cbl.Items);
        var allNormalizedNames = nameVariants.Keys.ToList();

        // Also include direct normalized names for remap rule lookup
        var directNormalizedNames = cbl.Items
            .Select(i => i.SeriesName.ToNormalized())
            .Distinct()
            .ToList();

        foreach (var n in directNormalizedNames)
        {
            if (!allNormalizedNames.Contains(n)) allNormalizedNames.Add(n);
        }

        // Collect external IDs
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

        // TODO: Figure out if I want to keep this in final design
        if (options.ApplicableLibraries is { Count: > 0 })
        {
            userLibraryIds = userLibraryIds.Where(options.ApplicableLibraries.Contains).ToList();
        }

        // Batch DB queries
        var remapRules = await unitOfWork.RemapRuleRepository
            .GetRulesForNamesAsync(directNormalizedNames, userId);

        var externalIdChapters = await unitOfWork.ChapterRepository
            .GetChaptersByExternalIdsAsync(comicVineIds, metronIds, userLibraryIds);

        var matchedSeries = (await unitOfWork.SeriesRepository
            .GetAllSeriesByNameAsync(allNormalizedNames, userId, options.ApplicableLibraries,
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
            matchedSeries, alternateSeriesChapters, options);
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
