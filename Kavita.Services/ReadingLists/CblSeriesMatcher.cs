using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Kavita.Common.Extensions;
using Kavita.Models.DTOs.ReadingLists.CBL;
using Kavita.Models.DTOs.ReadingLists.CBL.Import;
using Kavita.Models.DTOs.ReadingLists.CBL.Internal;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.ReadingLists;
using Kavita.Services.Extensions;
using Kavita.Services.Helpers;
using Kavita.Services.Scanner;

namespace Kavita.Services.ReadingLists;

/// <summary>
/// Result of matching a single CBL item to Kavita entities
/// </summary>
internal sealed record MatchedItem(int SeriesId, int VolumeId, int ChapterId, CblMatchTier SeriesTier);

/// <summary>
/// Pure matching logic — takes pre-fetched data, returns per-item resolutions. No DB access.
/// </summary>
internal static class CblSeriesMatcher
{
    private static readonly string[] ReprintSuffixes =
    [
        "director's cut", "directors cut", "deluxe edition", "deluxe",
        "omnibus edition", "omnibus", "tpb", "trade paperback",
        "hc", "hardcover", "complete edition", "absolute",
        "new edition", "revised edition", "anniversary edition",
        "collected edition", "compendium", "gallery edition",
        "artist's edition", "artists edition"
    ];

    /// <summary>
    /// Generates all normalized name variants for a set of CBL items, mapping each variant
    /// back to the original series name and which tier generated it.
    /// </summary>
    public static Dictionary<string, (string OriginalName, CblMatchTier Tier)> GenerateAllNameVariants(IList<ParsedCblItem> items)
    {
        var variants = new Dictionary<string, (string, CblMatchTier)>();
        var uniqueNames = items.Select(i => i.SeriesName).Distinct().ToList();

        foreach (var name in uniqueNames)
        {
            // Exact normalized
            AddVariants(variants, name, CblMatchTier.ExactName, name);

            // Article stripped
            var sortTitle = BookSortTitlePrefixHelper.GetSortTitle(name);
            if (!string.Equals(sortTitle, name, StringComparison.OrdinalIgnoreCase))
            {
                AddVariants(variants, sortTitle, CblMatchTier.ArticleStripped, name);
            }

            // Reprint stripped
            var stripped = StripReprintSuffix(name);
            if (!string.Equals(stripped, name, StringComparison.OrdinalIgnoreCase))
            {
                AddVariants(variants, stripped, CblMatchTier.ReprintStripped, name);
            }
        }

        // Tier 3: Comic Vine handling — distinct by (series, volume) since different
        // volumes of the same series name produce different naming variants
        // (e.g. "Batman" vol 2014 -> "Batman (2014)", "Batman" vol 1994 -> "Batman (1994)")
        var uniqueSeriesVolumes = items
            .Where(i => !string.IsNullOrEmpty(i.Volume))
            .DistinctBy(i => (i.SeriesName, i.Volume))
            .ToList();

        foreach (var item in uniqueSeriesVolumes)
        {
            var comicVineTitle = GetComicNamingPattern(item.SeriesName, item.Volume);
            if (!string.Equals(comicVineTitle, item.SeriesName, StringComparison.OrdinalIgnoreCase))
            {
                AddVariants(variants, comicVineTitle, CblMatchTier.ComicVineNaming, item.SeriesName);
            }
        }

        return variants;
    }

    /// <summary>
    /// Main matching entry point. Resolves all CBL items against pre-fetched data.
    /// </summary>
    public static Dictionary<int, (MatchedItem? Match, CblBookResult Result)> ResolveAll(
        IList<ParsedCblItem> items,
        IList<ReadingListRemapRule> remapRules,
        IList<Chapter> externalIdChapters,
        IList<Series> matchedSeries,
        IList<Chapter> alternateSeriesChapters)
    {
        var results = new Dictionary<int, (MatchedItem? Match, CblBookResult Result)>();

        // Build lookup structures
        var rulesByName = remapRules
            .GroupBy(r => r.NormalizedCblSeriesName)
            .ToDictionary(g => g.Key, g => g.ToList());

        var externalIdByComicVine = externalIdChapters
            .Where(c => !string.IsNullOrEmpty(c.ComicVineId))
            .GroupBy(c => c.ComicVineId!)
            .ToDictionary(g => g.Key, g => g.ToList());

        var externalIdByMetron = externalIdChapters
            .Where(c => c.MetronId > 0)
            .GroupBy(c => c.MetronId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var externalIdByKavitaId = externalIdChapters
            .ToDictionary(c => c.Id, c => c);

        var nameVariants = GenerateAllNameVariants(items);

        // Build series lookup: normalized name -> list of series
        var seriesByNormalizedName = new Dictionary<string, List<Series>>();
        foreach (var series in matchedSeries)
        {
            AddToLookup(seriesByNormalizedName, series.NormalizedName, series);
            if (!string.IsNullOrEmpty(series.NormalizedLocalizedName) &&
                series.NormalizedLocalizedName != series.NormalizedName)
            {
                AddToLookup(seriesByNormalizedName, series.NormalizedLocalizedName, series);
            }
        }

        var altSeriesByNormName = alternateSeriesChapters
            .GroupBy(c => c.AlternateSeries.ToNormalized())
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var item in items)
        {
            var normalizedName = item.SeriesName.ToNormalized();

            // Tier 0: Remap rules
            if (TryMatchByRemapRule(item, normalizedName, rulesByName, matchedSeries, out var remapResult))
            {
                results[item.Order] = remapResult!.Value;
                continue;
            }

            // Tier 1: External IDs
            if (TryMatchByExternalId(item, externalIdByKavitaId, externalIdByComicVine, externalIdByMetron, out var extMatch, out var extChapter))
            {
                results[item.Order] = (extMatch, new CblBookResult(item)
                {
                    Reason = CblImportReason.Success,
                    MatchTier = CblMatchTier.ExternalId,
                    SeriesId = extMatch.SeriesId,
                    LibraryId = extChapter.Volume.Series?.LibraryId ?? 0,
                    ChapterId = extChapter.Id,
                    ChapterTitle = !string.IsNullOrEmpty(extChapter.TitleName) ? extChapter.TitleName : extChapter.Range,
                    ChapterNumber = extChapter.Range,
                    MatchedSeriesName = extChapter.Volume.Series?.Name ?? string.Empty,
                    LibraryType = extChapter.Volume.Series?.Library?.Type ?? LibraryType.Comic
                });
                continue;
            }

            // Tiers 2-4: Name matching
            if (TryMatchByName(item, nameVariants, seriesByNormalizedName, out var seriesMatch, out var tier))
            {
                // Series resolved, now resolve chapter
                results[item.Order] = ResolveChapter(item, seriesMatch, tier);
                continue;
            }

            // Tier 5: AlternateSeries
            if (TryMatchByAlternateSeries(item, normalizedName, altSeriesByNormName, out var altMatch, out var altChapter))
            {
                results[item.Order] = (altMatch, new CblBookResult(item)
                {
                    Reason = CblImportReason.Success,
                    MatchTier = CblMatchTier.AlternateSeries,
                    SeriesId = altMatch.SeriesId,
                    LibraryId = altChapter.Volume.Series?.LibraryId ?? 0,
                    ChapterId = altChapter.Id,
                    ChapterTitle = !string.IsNullOrEmpty(altChapter.TitleName) ? altChapter.TitleName : altChapter.Range,
                    ChapterNumber = altChapter.Range,
                    MatchedSeriesName = altChapter.Volume.Series?.Name ?? string.Empty,
                    LibraryType = altChapter.Volume.Series?.Library?.Type ?? LibraryType.Comic
                });
                continue;
            }

            // Tier 6: Unmatched
            results[item.Order] = (null, new CblBookResult(item) { Reason = CblImportReason.SeriesMissing, MatchTier = CblMatchTier.Unmatched });
        }

        return results;
    }

    private static bool TryMatchByRemapRule(ParsedCblItem item, string normalizedName,
        Dictionary<string, List<ReadingListRemapRule>> rulesByName,
        IList<Series> matchedSeries,
        out (MatchedItem? Match, CblBookResult Result)? resolvedResult)
    {
        resolvedResult = null;
        if (!rulesByName.TryGetValue(normalizedName, out var rules)) return false;

        // Try most specific first (volume + number), then less specific
        var rule = rules.FirstMatchVolumeAndIssueOrDefault(item)
                   ?? rules.FirstMatchIssueOrDefault(item)
                   ?? rules.FirstMatchVolumeOrDefault(item)
                   ?? rules.FirstOrDefault(r =>
                       string.IsNullOrEmpty(r.CblVolume) && string.IsNullOrEmpty(r.CblNumber));

        if (rule == null) return false;

        if (rule is {ChapterId: not null, VolumeId: not null})
        {
            var chapterTitle = string.Empty;
            var chapterNumber = string.Empty;
            var libraryId = 0;
            var libraryType = LibraryType.Comic;
            var ruleSeries = matchedSeries.FirstOrDefault(s => s.Id == rule.SeriesId);

            if (ruleSeries != null)
            {
                libraryId = ruleSeries.LibraryId;
                libraryType = ruleSeries.Library?.Type ?? LibraryType.Comic;
                var ch = ruleSeries.Volumes?
                    .SelectMany(v => v.Chapters ?? [])
                    .FirstOrDefault(c => c.Id == rule.ChapterId.Value);
                if (ch != null)
                {
                    chapterTitle = !string.IsNullOrEmpty(ch.TitleName) ? ch.TitleName : ch.Range;
                    chapterNumber = ch.Range;
                }
            }

            resolvedResult = (
                new MatchedItem(rule.SeriesId, rule.VolumeId.Value, rule.ChapterId.Value, CblMatchTier.RemapRule),
                new CblBookResult(item)
                {
                    Reason = CblImportReason.Success,
                    MatchTier = CblMatchTier.RemapRule,
                    SeriesId = rule.SeriesId,
                    LibraryId = libraryId,
                    ChapterId = rule.ChapterId.Value,
                    ChapterTitle = chapterTitle,
                    ChapterNumber = chapterNumber,
                    MatchedSeriesName = ruleSeries?.Name ?? string.Empty,
                    LibraryType = libraryType
                }
            );
            return true;
        }

        // Volume-only remap with target VolumeId - resolve chapters within the override volume
        if (rule is {VolumeId: not null, ChapterId: null})
        {
            var volSeries = matchedSeries.FirstOrDefault(s => s.Id == rule.SeriesId);
            var targetVolume = volSeries?.Volumes?.FirstOrDefault(v => v.Id == rule.VolumeId.Value);

            if (targetVolume == null) return false;
            var resolved = ResolveChapter(item, volSeries!, CblMatchTier.RemapRule, targetVolume);
            if (resolved.Result.Reason is CblImportReason.ChapterMissing)
            {
                return false;
            }

            resolvedResult = resolved;
            return true;
        }

        // Rule only mapped to series — resolve chapter within the mapped series.
        // The user has explicitly declared this mapping, so we should resolve within
        // the target series rather than falling through to lower tiers (which can never
        // match a remapped name like "Zombie Tales" -> "Adventure Time").
        var series = matchedSeries.FirstOrDefault(s => s.Id == rule.SeriesId);
        if (series != null)
        {
            var resolved = ResolveChapter(item, series, CblMatchTier.RemapRule);

            // If the CBL volume doesn't exist in the target series, retry without
            // the volume so resolution falls back to the loose-leaf volume.
            // This handles cases like a manga series with only loose issues being
            // targeted by a remap from a Comic CBL entry that carries a year-volume.
            if (resolved.Result.Reason is CblImportReason.VolumeMissing
                && !string.IsNullOrEmpty(item.Volume))
            {
                resolved = ResolveChapter(item with { Volume = string.Empty }, series, CblMatchTier.RemapRule);

                // Restore the original volume so the result shows what the CBL requested
                resolved.Result.Volume = item.Volume;

                // After the retry the remap is authoritative — return the result
                // (success or failure) rather than falling through to lower tiers
                // which would report SeriesMissing for a remapped name.
                resolvedResult = resolved;
                return true;
            }

            if (resolved.Result.Reason is CblImportReason.VolumeMissing or CblImportReason.ChapterMissing)
            {
                return false;
            }

            resolvedResult = resolved;
            return true;
        }

        // Series from the rule wasn't in our pre-fetched data — fall through to lower tiers
        return false;
    }

    private static bool TryMatchByExternalId(ParsedCblItem item,
        Dictionary<int, Chapter> byKavitaId,
        Dictionary<string, List<Chapter>> byComicVine,
        Dictionary<long, List<Chapter>> byMetron,
        out MatchedItem match, out Chapter matchedChapter)
    {
        foreach (var extId in item.ExternalIds)
        {
            if (extId.Provider == CblExternalDbProvider.Kavita
                && int.TryParse(extId.IssueId, out var kavitaChapterId))
            {
                if (byKavitaId.TryGetValue(kavitaChapterId, out var ch))
                {
                    match = new MatchedItem(ch.Volume.SeriesId, ch.VolumeId, ch.Id, CblMatchTier.ExternalId);
                    matchedChapter = ch;
                    return true;
                }
            }

            if (extId.Provider == CblExternalDbProvider.ComicVine && !string.IsNullOrEmpty(extId.IssueId))
            {
                if (byComicVine.TryGetValue(extId.IssueId, out var chapters) && chapters.Count > 0)
                {
                    var ch = chapters[0];
                    match = new MatchedItem(ch.Volume.SeriesId, ch.VolumeId, ch.Id, CblMatchTier.ExternalId);
                    matchedChapter = ch;
                    return true;
                }
            }

            if (extId.Provider == CblExternalDbProvider.Metron && long.TryParse(extId.IssueId, out var metronId) && metronId > 0)
            {
                if (byMetron.TryGetValue(metronId, out var chapters) && chapters.Count > 0)
                {
                    var ch = chapters[0];
                    match = new MatchedItem(ch.Volume.SeriesId, ch.VolumeId, ch.Id, CblMatchTier.ExternalId);
                    matchedChapter = ch;
                    return true;
                }
            }
        }

        match = null!;
        matchedChapter = null!;
        return false;
    }

    private static bool TryMatchByName(ParsedCblItem item,
        Dictionary<string, (string OriginalName, CblMatchTier Tier)> nameVariants,
        Dictionary<string, List<Series>> seriesByNormalizedName,
        out Series series, out CblMatchTier tier)
    {
        // Try each tier in order
        foreach (var candidateTier in new[] { CblMatchTier.ExactName, CblMatchTier.ComicVineNaming, CblMatchTier.ArticleStripped, CblMatchTier.ReprintStripped })
        {
            // For ComicVineNaming, use only the variant derived from this item's volume
            // to avoid cross-matching (e.g. "Batman" vol 1994 matching "Batman (2014)")
            List<string> variantsForTier;
            if (candidateTier == CblMatchTier.ComicVineNaming && !string.IsNullOrEmpty(item.Volume))
            {
                var comicVineVariant = GetComicNamingPattern(item.SeriesName, item.Volume).ToNormalized();
                variantsForTier = !string.IsNullOrEmpty(comicVineVariant) ? [comicVineVariant] : [];
            }
            else
            {
                variantsForTier = nameVariants
                    .Where(kv => kv.Value.Tier == candidateTier &&
                                 string.Equals(kv.Value.OriginalName, item.SeriesName, StringComparison.OrdinalIgnoreCase))
                    .Select(kv => kv.Key)
                    .ToList();
            }

            foreach (var variant in variantsForTier)
            {
                if (!seriesByNormalizedName.TryGetValue(variant, out var candidates) || candidates.Count == 0)
                    continue;

                tier = candidateTier;

                if (candidates.Count == 1)
                {
                    series = candidates[0];
                    return true;
                }

                // Disambiguate
                var disambiguated = DisambiguateSeries(candidates, item);
                if (disambiguated != null)
                {
                    series = disambiguated;
                    return true;
                }

                // Still ambiguous - take first, collision handled by caller through chapter resolution
                series = candidates[0];
                return true;
            }
        }

        series = null!;
        tier = CblMatchTier.Unmatched;
        return false;
    }

    private static Series? DisambiguateSeries(List<Series> candidates, ParsedCblItem item)
    {
        // Match by year if available
        if (int.TryParse(item.Year, out var year) && year > 0)
        {
            var yearFiltered = candidates.Where(s =>
                s.Metadata != null && s.Metadata.ReleaseYear == year).ToList();
            if (yearFiltered.Count == 1) return yearFiltered[0];
        }

        // Still ambiguous
        return candidates.Count == 1 ? candidates[0] : null;
    }

    private static (MatchedItem? Match, CblBookResult Result) ResolveChapter(
        ParsedCblItem item, Series series, CblMatchTier tier, Volume? overrideVolume = null)
    {
        var seriesLibraryType = series.Library?.Type ?? LibraryType.Comic;

        var volumes = series.Volumes;
        if (volumes == null || volumes.Count == 0)
        {
            return (null, new CblBookResult(item)
            {
                Reason = CblImportReason.VolumeMissing,
                MatchTier = tier,
                SeriesId = series.Id,
                LibraryId = series.LibraryId,
                MatchedSeriesName = series.Name,
                LibraryType = seriesLibraryType
            });
        }

        // Find the target volume
        Volume? targetVolume = null;
        var volumeWasRequested = !string.IsNullOrEmpty(item.Volume);

        if (overrideVolume != null)
        {
            targetVolume = overrideVolume;
            volumeWasRequested = true;
        }
        else if (volumeWasRequested)
        {
            // Try to find by volume name/number
            if (float.TryParse(item.Volume, NumberStyles.Any, CultureInfo.InvariantCulture, out var volNum))
            {
                targetVolume = volumes.FirstOrDefault(v =>
                    v.MinNumber <= volNum && v.MaxNumber >= volNum && !v.MinNumber.Is(Parser.SpecialVolumeNumber));
            }

            targetVolume ??= volumes.FirstOrDefault(v =>
                string.Equals(v.Name, item.Volume, StringComparison.OrdinalIgnoreCase));

            // Volume was explicitly requested but not found, report as VolumeMissing
            if (targetVolume == null)
            {
                return (null, new CblBookResult(item)
                {
                    Reason = CblImportReason.VolumeMissing,
                    MatchTier = tier,
                    SeriesId = series.Id,
                    LibraryId = series.LibraryId,
                    MatchedSeriesName = series.Name,
                    LibraryType = seriesLibraryType
                });
            }
        }
        else
        {
            // No volume specified, use loose-leaf
            targetVolume = volumes.GetLooseLeafVolumeOrDefault();
        }

        var fallbackVolume = volumes.GetSpecialVolumeOrDefault();

        // Try to find chapter
        Chapter? chapter = null;

        if (!string.IsNullOrEmpty(item.Number))
        {
            // Exact range match in target volume
            if (targetVolume?.Chapters != null)
            {
                chapter = targetVolume.Chapters.FirstOrDefault(c =>
                    string.Equals(c.Range, item.Number, StringComparison.OrdinalIgnoreCase));

                // Numeric match
                if (chapter == null && float.TryParse(item.Number, NumberStyles.Any, CultureInfo.InvariantCulture, out var chNum))
                {
                    chapter = targetVolume.Chapters.FirstOrDefault(c =>
                        c.MinNumber <= chNum && c.MaxNumber >= chNum);
                }
            }

            // Try fallback volume (specials) — only when no specific volume was requested
            if (chapter == null && !volumeWasRequested && fallbackVolume?.Chapters != null && fallbackVolume != targetVolume)
            {
                chapter = fallbackVolume.Chapters.FirstOrDefault(c =>
                    string.Equals(c.Range, item.Number, StringComparison.OrdinalIgnoreCase));

                if (chapter == null && float.TryParse(item.Number, NumberStyles.Any, CultureInfo.InvariantCulture, out var chNum2))
                {
                    chapter = fallbackVolume.Chapters.FirstOrDefault(c =>
                        c.MinNumber <= chNum2 && c.MaxNumber >= chNum2);
                }

                if (chapter != null) targetVolume = fallbackVolume;
            }

            // Search across all volumes as last resort — only when no specific volume was requested
            if (chapter == null && !volumeWasRequested)
            {
                foreach (var vol in volumes.Where(v => v != targetVolume && v != fallbackVolume))
                {
                    if (vol.Chapters == null) continue;
                    chapter = vol.Chapters.FirstOrDefault(c =>
                        string.Equals(c.Range, item.Number, StringComparison.OrdinalIgnoreCase));

                    if (chapter == null && float.TryParse(item.Number, NumberStyles.Any, CultureInfo.InvariantCulture, out var chNum3))
                    {
                        chapter = vol.Chapters.FirstOrDefault(c =>
                            c.MinNumber <= chNum3 && c.MaxNumber >= chNum3);
                    }

                    if (chapter != null)
                    {
                        targetVolume = vol;
                        break;
                    }
                }
            }
        }
        else
        {
            // No issue number — default chapter in the volume
            if (targetVolume?.Chapters is { Count: > 0 })
            {
                chapter = targetVolume.Chapters.OrderBy(c => c.SortOrder).First();
            }
        }

        if (chapter == null)
        {
            return (null, new CblBookResult(item)
            {
                Reason = CblImportReason.ChapterMissing,
                MatchTier = tier,
                SeriesId = series.Id,
                LibraryId = series.LibraryId,
                MatchedSeriesName = series.Name,
                LibraryType = seriesLibraryType
            });
        }

        return (
            new MatchedItem(series.Id, targetVolume!.Id, chapter.Id, tier),
            new CblBookResult(item)
            {
                Reason = CblImportReason.Success,
                MatchTier = tier,
                SeriesId = series.Id,
                LibraryId = series.LibraryId,
                ChapterId = chapter.Id,
                ChapterTitle = !string.IsNullOrEmpty(chapter.TitleName) ? chapter.TitleName : chapter.Range,
                ChapterNumber = chapter.Range,
                MatchedSeriesName = series.Name,
                LibraryType = seriesLibraryType
            }
        );
    }

    private static bool TryMatchByAlternateSeries(ParsedCblItem item, string normalizedName,
        Dictionary<string, List<Chapter>> altSeriesByNormName, out MatchedItem match, out Chapter matchedChapter)
    {
        match = null!;
        matchedChapter = null!;
        if (!altSeriesByNormName.TryGetValue(normalizedName, out var chapters) || chapters.Count == 0) return false;

        // Try to find matching chapter by number
        if (!string.IsNullOrEmpty(item.Number))
        {
            var found = chapters.FirstOrDefault(c =>
                string.Equals(c.Range, item.Number, StringComparison.OrdinalIgnoreCase));

            if (found == null && float.TryParse(item.Number, NumberStyles.Any, CultureInfo.InvariantCulture, out var chNum))
            {
                found = chapters.FirstOrDefault(c => c.MinNumber <= chNum && c.MaxNumber >= chNum);
            }

            if (found != null)
            {
                match = new MatchedItem(found.Volume.SeriesId, found.VolumeId, found.Id, CblMatchTier.AlternateSeries);
                matchedChapter = found;
                return true;
            }
        }

        // Just take the first one if no number specified
        if (string.IsNullOrEmpty(item.Number) && chapters.Count > 0)
        {
            var ch = chapters[0];
            match = new MatchedItem(ch.Volume.SeriesId, ch.VolumeId, ch.Id, CblMatchTier.AlternateSeries);
            matchedChapter = ch;
            return true;
        }

        return false;
    }

    private static void AddVariants(Dictionary<string, (string, CblMatchTier)> variants,
        string name, CblMatchTier tier, string originalName)
    {
        var normalized = name.ToNormalized();
        if (!string.IsNullOrEmpty(normalized))
        {
            variants.TryAdd(normalized, (originalName, tier));
        }
    }

    private static string GetComicNamingPattern(string name, string volumeName)
    {
        var trimmed = name.Trim();
        return $"{trimmed} ({volumeName})";
    }

    private static string StripReprintSuffix(string name)
    {
        var trimmed = name.Trim();
        foreach (var suffix in ReprintSuffixes)
        {
            if (!trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;
            var stripped = trimmed[..^suffix.Length].TrimEnd(' ', '-', ':');
            if (!string.IsNullOrWhiteSpace(stripped)) return stripped;
        }

        return name;
    }

    private static void AddToLookup<TKey, TValue>(Dictionary<TKey, List<TValue>> dict, TKey key, TValue value) where TKey : notnull
    {
        if (!dict.TryGetValue(key, out var list))
        {
            list = [];
            dict[key] = list;
        }
        list.Add(value);
    }
}
