using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Kavita.Common.Extensions;
using Kavita.Models.DTOs.ReadingLists.CBL;
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
            // Tier 2: Exact normalized
            AddVariants(variants, name, CblMatchTier.ExactName, name);

            // Tier 3: Article stripped
            var sortTitle = BookSortTitlePrefixHelper.GetSortTitle(name);
            if (!string.Equals(sortTitle, name, StringComparison.OrdinalIgnoreCase))
            {
                AddVariants(variants, sortTitle, CblMatchTier.ArticleStripped, name);
            }

            // Tier 4: Reprint stripped
            var stripped = StripReprintSuffix(name);
            if (!string.Equals(stripped, name, StringComparison.OrdinalIgnoreCase))
            {
                AddVariants(variants, stripped, CblMatchTier.ReprintStripped, name);
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
        IList<Chapter> alternateSeriesChapters,
        CblImportOptions options)
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
            if (TryMatchByExternalId(item, externalIdByComicVine, externalIdByMetron, out var extMatch))
            {
                results[item.Order] = (extMatch, new CblBookResult(item) { Reason = CblImportReason.Success, MatchTier = CblMatchTier.ExternalId });
                continue;
            }

            // Tiers 2-4: Name matching
            if (TryMatchByName(item, nameVariants, seriesByNormalizedName, options, out var seriesMatch, out var tier))
            {
                // Series resolved, now resolve chapter
                results[item.Order] = ResolveChapter(item, seriesMatch, tier);
                continue;
            }

            // Tier 5: AlternateSeries
            if (TryMatchByAlternateSeries(item, normalizedName, altSeriesByNormName, out var altMatch))
            {
                results[item.Order] = (altMatch, new CblBookResult(item) { Reason = CblImportReason.Success, MatchTier = CblMatchTier.AlternateSeries });
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
        var rule = rules.FirstOrDefault(r =>
                       !string.IsNullOrEmpty(r.CblVolume) && r.CblVolume == item.Volume &&
                       !string.IsNullOrEmpty(r.CblNumber) && r.CblNumber == item.Number)
                   ?? rules.FirstOrDefault(r =>
                       !string.IsNullOrEmpty(r.CblNumber) && r.CblNumber == item.Number &&
                       string.IsNullOrEmpty(r.CblVolume))
                   ?? rules.FirstOrDefault(r =>
                       string.IsNullOrEmpty(r.CblVolume) && string.IsNullOrEmpty(r.CblNumber));

        if (rule == null) return false;

        if (rule.ChapterId.HasValue && rule.VolumeId.HasValue)
        {
            resolvedResult = (
                new MatchedItem(rule.SeriesId, rule.VolumeId.Value, rule.ChapterId.Value, CblMatchTier.RemapRule),
                new CblBookResult(item) { Reason = CblImportReason.Success, MatchTier = CblMatchTier.RemapRule }
            );
            return true;
        }

        // Rule only mapped to series — resolve chapter within the mapped series
        var series = matchedSeries.FirstOrDefault(s => s.Id == rule.SeriesId);
        if (series != null)
        {
            resolvedResult = ResolveChapter(item, series, CblMatchTier.RemapRule);
            return true;
        }

        // Series from the rule wasn't in our pre-fetched data — report as series matched but chapter unresolved
        resolvedResult = (null, new CblBookResult(item)
        {
            Reason = CblImportReason.ChapterMissing,
            MatchTier = CblMatchTier.RemapRule,
            SeriesId = rule.SeriesId
        });
        return true;
    }

    private static bool TryMatchByExternalId(ParsedCblItem item,
        Dictionary<string, List<Chapter>> byComicVine,
        Dictionary<long, List<Chapter>> byMetron,
        out MatchedItem match)
    {
        foreach (var extId in item.ExternalIds)
        {
            if (extId.Provider == CblExternalDbProvider.ComicVine && !string.IsNullOrEmpty(extId.IssueId))
            {
                if (byComicVine.TryGetValue(extId.IssueId, out var chapters) && chapters.Count > 0)
                {
                    var ch = chapters[0];
                    match = new MatchedItem(ch.Volume.SeriesId, ch.VolumeId, ch.Id, CblMatchTier.ExternalId);
                    return true;
                }
            }

            if (extId.Provider == CblExternalDbProvider.Metron && long.TryParse(extId.IssueId, out var metronId) && metronId > 0)
            {
                if (byMetron.TryGetValue(metronId, out var chapters) && chapters.Count > 0)
                {
                    var ch = chapters[0];
                    match = new MatchedItem(ch.Volume.SeriesId, ch.VolumeId, ch.Id, CblMatchTier.ExternalId);
                    return true;
                }
            }
        }

        match = null!;
        return false;
    }

    private static bool TryMatchByName(ParsedCblItem item,
        Dictionary<string, (string OriginalName, CblMatchTier Tier)> nameVariants,
        Dictionary<string, List<Series>> seriesByNormalizedName,
        CblImportOptions options,
        out Series series, out CblMatchTier tier)
    {
        // Try each tier in order
        foreach (var candidateTier in new[] { CblMatchTier.ExactName, CblMatchTier.ArticleStripped, CblMatchTier.ReprintStripped })
        {
            var variantsForTier = nameVariants
                .Where(kv => kv.Value.Tier == candidateTier &&
                             string.Equals(kv.Value.OriginalName, item.SeriesName, StringComparison.OrdinalIgnoreCase))
                .Select(kv => kv.Key)
                .ToList();

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
                var disambiguated = DisambiguateSeries(candidates, item, options);
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

    private static Series? DisambiguateSeries(List<Series> candidates, ParsedCblItem item, CblImportOptions options)
    {
        var filtered = candidates;

        // Filter by applicable libraries
        if (options.ApplicableLibraries is { Count: > 0 })
        {
            var libFiltered = filtered.Where(s => options.ApplicableLibraries.Contains(s.LibraryId)).ToList();
            if (libFiltered.Count > 0) filtered = libFiltered;
        }

        if (filtered.Count == 1) return filtered[0];

        // Prefer Comic library type if PreferComicVineMatching
        if (options.PreferComicVineMatching)
        {
            var comicFiltered = filtered.Where(s => s.Library != null &&
                (s.Library.Type == LibraryType.Comic || s.Library.Type == LibraryType.ComicVine)).ToList();
            if (comicFiltered.Count > 0) filtered = comicFiltered;
        }

        if (filtered.Count == 1) return filtered[0];

        // Match by year if available
        if (int.TryParse(item.Year, out var year) && year > 0)
        {
            var yearFiltered = filtered.Where(s =>
                s.Metadata != null && s.Metadata.ReleaseYear == year).ToList();
            if (yearFiltered.Count == 1) return yearFiltered[0];
        }

        // Still ambiguous
        return filtered.Count == 1 ? filtered[0] : null;
    }

    private static (MatchedItem? Match, CblBookResult Result) ResolveChapter(ParsedCblItem item, Series series, CblMatchTier tier)
    {
        var volumes = series.Volumes;
        if (volumes == null || volumes.Count == 0)
        {
            return (null, new CblBookResult(item)
            {
                Reason = CblImportReason.VolumeMissing,
                MatchTier = tier,
                SeriesId = series.Id,
                LibraryId = series.LibraryId
            });
        }

        // Find the target volume
        Volume? targetVolume = null;
        if (!string.IsNullOrEmpty(item.Volume))
        {
            // Try to find by volume name/number
            if (float.TryParse(item.Volume, NumberStyles.Any, CultureInfo.InvariantCulture, out var volNum))
            {
                targetVolume = volumes.FirstOrDefault(v =>
                    v.MinNumber <= volNum && v.MaxNumber >= volNum && !v.MinNumber.Is(Parser.SpecialVolumeNumber));
            }

            targetVolume ??= volumes.FirstOrDefault(v =>
                string.Equals(v.Name, item.Volume, StringComparison.OrdinalIgnoreCase));
        }

        // Fallback to loose leaf volume, then specials
        targetVolume ??= volumes.GetLooseLeafVolumeOrDefault();
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

            // Try fallback volume (specials)
            if (chapter == null && fallbackVolume?.Chapters != null && fallbackVolume != targetVolume)
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

            // Search across all volumes as last resort
            if (chapter == null)
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
                LibraryId = series.LibraryId
            });
        }

        return (
            new MatchedItem(series.Id, targetVolume!.Id, chapter.Id, tier),
            new CblBookResult(item) { Reason = CblImportReason.Success, MatchTier = tier, SeriesId = series.Id, LibraryId = series.LibraryId }
        );
    }

    private static bool TryMatchByAlternateSeries(ParsedCblItem item, string normalizedName,
        Dictionary<string, List<Chapter>> altSeriesByNormName, out MatchedItem match)
    {
        match = null!;
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
                return true;
            }
        }

        // Just take the first one if no number specified
        if (string.IsNullOrEmpty(item.Number) && chapters.Count > 0)
        {
            var ch = chapters[0];
            match = new MatchedItem(ch.Volume.SeriesId, ch.VolumeId, ch.Id, CblMatchTier.AlternateSeries);
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

    private static string StripReprintSuffix(string name)
    {
        var trimmed = name.Trim();
        foreach (var suffix in ReprintSuffixes)
        {
            if (trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                var stripped = trimmed[..^suffix.Length].TrimEnd(' ', '-', ':');
                if (!string.IsNullOrWhiteSpace(stripped))
                    return stripped;
            }
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
