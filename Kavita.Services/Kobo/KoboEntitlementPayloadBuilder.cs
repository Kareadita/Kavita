using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Kavita.Common.Extensions;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Metadata;
using Kavita.Models.Entities.Person;
using Kavita.Models.Entities.User;
using Kavita.Services.Scanner;

namespace Kavita.Services.Kobo;

/// <summary>
/// Builds the BookEntitlement / BookMetadata JSON shells the Kobo store API expects for a chapter.
/// Pure payload construction: any instance-bound data (e.g. cached KEPUB paths) is supplied by callback.
/// </summary>
public static class KoboEntitlementPayloadBuilder
{
    /// <summary>Placeholder genre/category id emitted for every entitlement.</summary>
    public static readonly Guid EmptyGenreId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>
    /// Full entitlement envelope (BookEntitlement + BookMetadata) for a live chapter.
    /// </summary>
    public static async Task<JsonObject> BuildEntitlementPayloadAsync(Chapter chapter, Series series,
        string entitlementUuid, string tokenBase, bool isRemoved, bool preferKepub,
        Func<int, MangaFile, Task<string?>> tryGetCachedKepubPathAsync)
    {
        return new JsonObject
        {
            ["BookEntitlement"] = BuildBookEntitlement(chapter, entitlementUuid, isRemoved),
            ["BookMetadata"] = await BuildBookMetadataAsync(chapter, series, entitlementUuid, tokenBase,
                preferKepub, tryGetCachedKepubPathAsync),
        };
    }

    /// <summary>
    /// Removal envelope for a chapter that no longer exists (hard-delete tombstone).
    /// </summary>
    public static JsonObject BuildTombstoneEntitlementPayload(AppUserKoboTombstone tombstone,
        string entitlementUuid)
    {
        var now = KoboDateTime.FormatTimestamp(DateTime.UtcNow);
        var created = KoboDateTime.FormatTimestamp(tombstone.CreatedUtc);

        var metadata = BuildBookMetadataShell(entitlementUuid, tombstone.Title, description: null,
            downloadUrls: new JsonArray(), language: "en", publisherName: null);
        metadata["Contributors"] = null;

        return new JsonObject
        {
            ["BookEntitlement"] = new JsonObject
            {
                ["Accessibility"] = "Full",
                ["ActivePeriod"] = new JsonObject { ["From"] = now },
                ["Created"] = created,
                ["CrossRevisionId"] = entitlementUuid,
                ["Id"] = entitlementUuid,
                ["IsRemoved"] = true,
                ["IsHiddenFromArchive"] = false,
                ["IsLocked"] = false,
                ["LastModified"] = now,
                ["OriginCategory"] = "Imported",
                ["RevisionId"] = entitlementUuid,
                ["Status"] = "Active",
            },
            ["BookMetadata"] = metadata,
        };
    }

    private static JsonObject BuildBookEntitlement(Chapter chapter, string entitlementUuid, bool isRemoved)
    {
        var created = KoboDateTime.FormatTimestamp(KoboDateTime.CoalesceUtc(chapter.CreatedUtc, chapter.Created));
        var modified = KoboDateTime.FormatTimestamp(
            chapter.LastModifiedUtc == default ? chapter.LastModified : chapter.LastModifiedUtc);

        return new JsonObject
        {
            ["Accessibility"] = "Full",
            ["ActivePeriod"] = new JsonObject { ["From"] = KoboDateTime.FormatTimestamp(DateTime.UtcNow) },
            ["Created"] = created,
            ["CrossRevisionId"] = entitlementUuid,
            ["Id"] = entitlementUuid,
            ["IsRemoved"] = isRemoved,
            ["IsHiddenFromArchive"] = false,
            ["IsLocked"] = false,
            ["LastModified"] = modified,
            ["OriginCategory"] = "Imported",
            ["RevisionId"] = entitlementUuid,
            ["Status"] = "Active",
        };
    }

    public static async Task<JsonObject> BuildBookMetadataAsync(Chapter chapter, Series series,
        string entitlementUuid, string tokenBase, bool preferKepub,
        Func<int, MangaFile, Task<string?>> tryGetCachedKepubPathAsync)
    {
        var downloadUrls = await BuildDownloadUrlsAsync(chapter, entitlementUuid, tokenBase, preferKepub,
            tryGetCachedKepubPathAsync);

        var description = !string.IsNullOrWhiteSpace(chapter.Summary)
            ? chapter.Summary
            : series.Metadata?.Summary;
        var language = !string.IsNullOrWhiteSpace(series.Metadata?.Language)
            ? series.Metadata!.Language
            : "en";
        var publisher = ResolvePublisher(chapter, series.Metadata);

        var metadata = BuildBookMetadataShell(entitlementUuid, BuildTitle(series, chapter), description,
            downloadUrls, language, publisher);
        metadata["Series"] = BuildSeriesMetadata(series, chapter);

        if (chapter.ReleaseDate != default)
        {
            metadata["PublicationDate"] = KoboDateTime.FormatTimestamp(chapter.ReleaseDate);
        }

        var writers = ResolveWriters(chapter, series.Metadata);
        if (writers.Count > 0)
        {
            var roles = new JsonArray();
            var names = new JsonArray();
            foreach (var writer in writers)
            {
                roles.Add(new JsonObject { ["Name"] = writer });
                names.Add(writer);
            }

            metadata["ContributorRoles"] = roles;
            metadata["Contributors"] = names;
        }
        else
        {
            metadata["Contributors"] = null;
        }

        return metadata;
    }

    /// <summary>
    /// Shared BookMetadata scaffold for both live and tombstone entitlements. Callers layer on
    /// Series / PublicationDate / Contributors after.
    /// </summary>
    private static JsonObject BuildBookMetadataShell(string entitlementUuid, string title, string? description,
        JsonArray downloadUrls, string language, string? publisherName) => new()
    {
        ["Categories"] = new JsonArray { EmptyGenreId.ToString() },
        ["CoverImageId"] = entitlementUuid,
        ["CrossRevisionId"] = entitlementUuid,
        ["CurrentDisplayPrice"] = new JsonObject
        {
            ["CurrencyCode"] = "USD",
            ["TotalAmount"] = 0,
        },
        ["CurrentLoveDisplayPrice"] = new JsonObject { ["TotalAmount"] = 0 },
        ["Description"] = description,
        ["DownloadUrls"] = downloadUrls,
        ["EntitlementId"] = entitlementUuid,
        ["ExternalIds"] = new JsonArray(),
        ["Genre"] = EmptyGenreId.ToString(),
        ["IsEligibleForKoboLove"] = false,
        ["IsInternetArchive"] = false,
        ["IsPreOrder"] = false,
        ["IsSocialEnabled"] = true,
        ["Language"] = language,
        ["PhoneticPronunciations"] = new JsonObject(),
        ["Publisher"] = new JsonObject
        {
            ["Imprint"] = string.Empty,
            ["Name"] = publisherName,
        },
        ["RevisionId"] = entitlementUuid,
        ["Title"] = title,
        ["WorkId"] = entitlementUuid,
    };

    private static async Task<JsonArray> BuildDownloadUrlsAsync(Chapter chapter, string entitlementUuid,
        string tokenBase, bool preferKepub, Func<int, MangaFile, Task<string?>> tryGetCachedKepubPathAsync)
    {
        var downloadUrls = new JsonArray();
        var epub = KoboEligibleFormats.PreferNativeEpub(chapter.Files);
        var archive = KoboEligibleFormats.PreferConvertibleArchive(chapter.Files);

        // Immediate catalog presence: advertise EPUB download even when conversion is still pending.
        if (epub == null && archive == null) return downloadUrls;

        var source = epub ?? archive!;
        string? kepubPath = null;
        if (preferKepub)
        {
            // Library file already promoted to KEPUB: advertise kepub URL from the library path.
            if (epub != null && KoboConversionService.IsAlreadyKepubLibraryFile(epub))
            {
                kepubPath = epub.FilePath;
            }
            else
            {
                kepubPath = await tryGetCachedKepubPathAsync(chapter.Id, source);
            }
        }

        if (kepubPath != null)
        {
            long size = 0;
            try
            {
                size = new FileInfo(kepubPath).Length;
            }
            catch (IOException)
            {
                // Size is advisory; advertise KEPUB even if size cannot be read.
            }

            var kepubUrl = $"{tokenBase}/download/{entitlementUuid}/kepub";
            downloadUrls.Add(BuildDownloadUrl(KoboService.KepubFormat, size, kepubUrl));
        }
        else
        {
            var size = epub?.Bytes > 0 ? epub.Bytes : 0;
            var url = $"{tokenBase}/download/{entitlementUuid}/epub";
            // Advertise both so firmware that prefers EPUB3 still resolves a download.
            downloadUrls.Add(BuildDownloadUrl(KoboService.Epub3Format, size, url));
            downloadUrls.Add(BuildDownloadUrl(KoboService.EpubFormat, size, url));
        }

        return downloadUrls;
    }

    private static JsonObject BuildDownloadUrl(string format, long size, string url) => new()
    {
        ["Format"] = format,
        ["Size"] = size,
        ["Url"] = url,
        ["Platform"] = "Generic",
    };

    private static JsonObject BuildSeriesMetadata(Series series, Chapter chapter)
    {
        var seriesMeta = new JsonObject
        {
            ["Name"] = series.Name,
            ["Id"] = KoboEntitlementId.CreateVersion5(KoboEntitlementId.Namespace, $"series:{series.Name}")
                .ToString(),
        };

        // Omit placeholder/default chapter numbers so Kobo does not sort specials as -100000.
        if (chapter.MinNumber.IsNot(Parser.DefaultChapterNumber) && chapter.MinNumber > 0)
        {
            seriesMeta["Number"] = chapter.MinNumber;
            seriesMeta["NumberFloat"] = chapter.MinNumber;
        }

        return seriesMeta;
    }

    internal static string BuildTitle(Series series, Chapter chapter)
    {
        string chapterLabel;
        if (chapter.IsSpecial)
        {
            var special = !string.IsNullOrWhiteSpace(chapter.TitleName)
                ? chapter.TitleName
                : Parser.CleanSpecialTitle(chapter.Title);
            chapterLabel = string.IsNullOrWhiteSpace(special) ? chapter.Range : special;
        }
        else if (!string.IsNullOrWhiteSpace(chapter.TitleName))
        {
            chapterLabel = chapter.TitleName;
        }
        else if (!string.IsNullOrWhiteSpace(chapter.Title))
        {
            chapterLabel = chapter.Title;
        }
        else
        {
            chapterLabel = chapter.Range;
        }

        return $"{series.Name} - {chapterLabel}";
    }

    private static List<string> ResolveWriters(Chapter chapter, SeriesMetadata? metadata)
    {
        var chapterWriters = chapter.People
            .Where(p => p.Role == PersonRole.Writer)
            .Select(p => p.Person.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();
        if (chapterWriters.Count > 0) return chapterWriters;

        return metadata?.People
            .Where(p => p.Role == PersonRole.Writer)
            .Select(p => p.Person.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList() ?? [];
    }

    private static string? ResolvePublisher(Chapter chapter, SeriesMetadata? metadata)
    {
        var chapterPublisher = chapter.People
            .FirstOrDefault(p => p.Role == PersonRole.Publisher)?.Person.Name;
        if (!string.IsNullOrWhiteSpace(chapterPublisher)) return chapterPublisher;

        return metadata?.People
            .FirstOrDefault(p => p.Role == PersonRole.Publisher)?.Person.Name;
    }
}
