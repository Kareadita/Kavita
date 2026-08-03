using System;
using System.Globalization;
using System.Text.Json.Nodes;
using Kavita.Models.Entities.Progress;
using Kavita.Models.Entities.User;

namespace Kavita.Services.Kobo;

/// <summary>
/// Maps between Kobo ReadingState wire shapes and Kavita <see cref="AppUserProgress"/> /
/// <see cref="AppUserKoboReadingLocation"/>. Statistics are ignored (not persisted).
/// </summary>
public static class KoboReadingStateMapper
{
    public const string StatusReadyToRead = "ReadyToRead";
    public const string StatusReading = "Reading";
    public const string StatusFinished = "Finished";

    public static string StatusFromPages(int pagesRead, int totalPages)
    {
        if (pagesRead <= 0) return StatusReadyToRead;
        if (totalPages > 0 && pagesRead >= totalPages) return StatusFinished;
        return StatusReading;
    }

    public static double PagesToProgressPercent(int pagesRead, int totalPages)
    {
        if (totalPages <= 0 || pagesRead <= 0) return 0;
        var pct = pagesRead / (double)totalPages * 100.0;
        // Calibre-Web emits whole-number floats as ints.
        return Math.Abs(pct - Math.Round(pct)) < 0.0001
            ? Math.Round(pct)
            : Math.Round(pct, 2, MidpointRounding.AwayFromZero);
    }

    public static int ProgressPercentToPages(double percent, int totalPages)
    {
        if (totalPages <= 0) return 0;
        var pages = (int)Math.Round(percent / 100.0 * totalPages, MidpointRounding.AwayFromZero);
        return Math.Clamp(pages, 0, totalPages);
    }

    public static int ResolvePagesRead(JsonObject? readingState, int totalPages, int existingPagesRead)
    {
        var bookmark = readingState?["CurrentBookmark"] as JsonObject;
        var statusInfo = readingState?["StatusInfo"] as JsonObject;
        var status = statusInfo?["Status"]?.GetValue<string>();

        if (TryGetProgressPercent(bookmark, out var percent))
        {
            return ProgressPercentToPages(percent, totalPages);
        }

        if (string.Equals(status, StatusFinished, StringComparison.OrdinalIgnoreCase))
        {
            return Math.Max(totalPages, 0);
        }

        if (string.Equals(status, StatusReadyToRead, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (string.Equals(status, StatusReading, StringComparison.OrdinalIgnoreCase) && existingPagesRead <= 0)
        {
            return totalPages > 0 ? 1 : 0;
        }

        return existingPagesRead;
    }

    /// <summary>
    /// Newest device timestamp from the PUT ReadingState (top-level or section LastModified).
    /// When absent, returns <paramref name="fallback"/> (typically UtcNow — device write wins).
    /// </summary>
    public static DateTime ResolveDeviceTimestamp(JsonObject? readingState, DateTime fallback)
    {
        DateTime? max = null;
        Consider(readingState?["LastModified"], ref max);
        if (readingState?["CurrentBookmark"] is JsonObject bookmark)
        {
            Consider(bookmark["LastModified"], ref max);
        }

        if (readingState?["StatusInfo"] is JsonObject statusInfo)
        {
            Consider(statusInfo["LastModified"], ref max);
        }

        if (readingState?["Statistics"] is JsonObject statistics)
        {
            Consider(statistics["LastModified"], ref max);
        }

        return max ?? fallback;
    }

    /// <summary>
    /// Reads <c>CurrentBookmark.Location</c>. Returns true when Value is truthy (Calibre-Web ingest).
    /// </summary>
    public static bool TryGetTruthyLocation(JsonObject? readingState,
        out string? value, out string? type, out string? source)
    {
        value = null;
        type = null;
        source = null;
        if (readingState?["CurrentBookmark"] is not JsonObject bookmark) return false;
        if (bookmark["Location"] is not JsonObject location) return false;

        value = ReadOptionalString(location["Value"]);
        if (string.IsNullOrEmpty(value)) return false;

        type = ReadOptionalString(location["Type"]);
        source = ReadOptionalString(location["Source"]);
        return true;
    }

    public static JsonObject BuildReadingState(string entitlementId, DateTime createdUtc,
        int pagesRead, int totalPages, DateTime lastModifiedUtc,
        AppUserKoboReadingLocation? location = null)
    {
        var created = FormatTimestamp(createdUtc == default ? lastModifiedUtc : createdUtc);
        var modified = FormatTimestamp(lastModifiedUtc == default ? DateTime.UtcNow : lastModifiedUtc);
        var status = StatusFromPages(pagesRead, totalPages);
        var percent = PagesToProgressPercent(pagesRead, totalPages);

        var bookmark = new JsonObject
        {
            ["LastModified"] = modified,
        };
        if (pagesRead > 0 || status != StatusReadyToRead)
        {
            bookmark["ProgressPercent"] = percent;
            bookmark["ContentSourceProgressPercent"] = percent;
        }

        if (location != null && !string.IsNullOrEmpty(location.LocationValue))
        {
            bookmark["Location"] = new JsonObject
            {
                ["Value"] = location.LocationValue,
                ["Type"] = JsonValue.Create(location.LocationType),
                ["Source"] = JsonValue.Create(location.LocationSource),
            };
        }

        return new JsonObject
        {
            ["EntitlementId"] = entitlementId,
            ["Created"] = created,
            ["LastModified"] = modified,
            ["PriorityTimestamp"] = modified,
            ["StatusInfo"] = new JsonObject
            {
                ["LastModified"] = modified,
                ["Status"] = status,
                ["TimesStartedReading"] = pagesRead > 0 ? 1 : 0,
            },
            ["Statistics"] = new JsonObject
            {
                ["LastModified"] = modified,
            },
            ["CurrentBookmark"] = bookmark,
        };
    }

    public static JsonObject BuildPutSuccess(string entitlementId, DateTime lastModifiedUtc,
        bool bookmarkApplied, bool statisticsApplied, bool statusApplied)
    {
        var modified = FormatTimestamp(lastModifiedUtc);
        var update = new JsonObject
        {
            ["EntitlementId"] = entitlementId,
            ["LastModified"] = modified,
            ["PriorityTimestamp"] = modified,
        };
        if (bookmarkApplied)
        {
            update["CurrentBookmarkResult"] = new JsonObject { ["Result"] = "Success" };
        }

        if (statisticsApplied)
        {
            update["StatisticsResult"] = new JsonObject { ["Result"] = "Success" };
        }

        if (statusApplied)
        {
            update["StatusInfoResult"] = new JsonObject { ["Result"] = "Success" };
        }

        return new JsonObject
        {
            ["RequestResult"] = "Success",
            ["UpdateResults"] = new JsonArray { update },
        };
    }

    public static string FormatTimestamp(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
        return utc.ToString("yyyy-MM-ddTHH:mm:ssZ");
    }

    public static bool TryParseTimestamp(JsonNode? node, out DateTime value)
    {
        value = default;
        if (node == null) return false;
        var text = node.GetValue<string>();
        if (string.IsNullOrWhiteSpace(text)) return false;

        if (DateTime.TryParse(text, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out value))
        {
            return true;
        }

        return false;
    }

    private static string? ReadOptionalString(JsonNode? node)
    {
        if (node == null) return null;
        try
        {
            return node.GetValue<string?>();
        }
        catch
        {
            return null;
        }
    }

    private static bool TryGetProgressPercent(JsonObject? bookmark, out double percent)
    {
        percent = 0;
        if (bookmark == null) return false;
        var node = bookmark["ProgressPercent"];
        if (node == null) return false;

        try
        {
            percent = node.GetValue<double>();
            return true;
        }
        catch
        {
            try
            {
                percent = node.GetValue<int>();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    private static void Consider(JsonNode? node, ref DateTime? max)
    {
        if (!TryParseTimestamp(node, out var ts)) return;
        if (max == null || ts > max.Value) max = ts;
    }
}
