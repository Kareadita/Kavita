using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Kavita.API.Services;
using Kavita.Models.Entities;
using Microsoft.Extensions.Logging;
using VersOne.Epub;
using static Kavita.Services.BookService;

namespace Kavita.Services.Kobo;

/// <summary>
/// Best-effort XPath ↔ Kobo Location mapper with in-file validation (VersOne + HtmlAgilityPack).
/// </summary>
public partial class KoboLocationMapper(ILogger<KoboLocationMapper> logger) : IKoboLocationMapper
{
    public const string TypeKoboSpan = "KoboSpan";

    [GeneratedRegex(@"^id\([""']([^""']+)[""']\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IdXPathRegex();

    [GeneratedRegex(@"^kobo\.\d+\.\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex KoboSpanIdRegex();

    public string? ResolveLibraryEpubPath(Chapter chapter)
    {
        var native = KoboService.PreferNativeEpub(chapter.Files);
        if (native == null || string.IsNullOrWhiteSpace(native.FilePath) || !File.Exists(native.FilePath))
        {
            return null;
        }

        return native.FilePath;
    }

    public string? ResolveDeviceOpenablePath(Chapter chapter, string? cachedKepubPath = null)
    {
        if (!string.IsNullOrWhiteSpace(cachedKepubPath) && File.Exists(cachedKepubPath))
        {
            return cachedKepubPath;
        }

        var native = KoboService.PreferNativeEpub(chapter.Files);
        // Archive-only converts stay percent-only for exact position (no Location invent).
        if (native == null) return null;

        if (string.IsNullOrWhiteSpace(native.FilePath) || !File.Exists(native.FilePath))
        {
            return null;
        }

        return native.FilePath;
    }

    public async Task<string?> TryMapLocationToBookScrollIdAsync(string? libraryEpubPath,
        string? locationValue, string? locationType, string? locationSource,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(libraryEpubPath) || string.IsNullOrWhiteSpace(locationValue))
        {
            return null;
        }

        if (!File.Exists(libraryEpubPath)) return null;

        try
        {
            using var book = await EpubReader.OpenBookAsync(libraryEpubPath, LenientBookReaderOptions);
            var readingOrder = (await book.GetReadingOrderAsync())
                .Where(c => c.ContentType == EpubContentType.XHTML_1_1)
                .ToList();
            if (readingOrder.Count == 0) return null;

            var target = await FindContentForSourceAsync(readingOrder, locationSource, ct);
            if (target != null)
            {
                var scrollId = await TryResolveBookScrollIdInContentAsync(target, locationValue, ct);
                if (scrollId != null) return scrollId;
            }

            // Source miss or id not in Source page: search the spine (still must exist in-file).
            foreach (var content in readingOrder)
            {
                ct.ThrowIfCancellationRequested();
                var scrollId = await TryResolveBookScrollIdInContentAsync(content, locationValue, ct);
                if (scrollId != null) return scrollId;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Kobo Location→BookScrollId map failed for {Path}", libraryEpubPath);
        }

        return null;
    }

    public async Task<KoboMappedLocation?> TryMapBookScrollIdToLocationAsync(string? deviceOpenablePath,
        int pageNum, string? bookScrollId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceOpenablePath) || string.IsNullOrWhiteSpace(bookScrollId))
        {
            return null;
        }

        if (!File.Exists(deviceOpenablePath)) return null;

        try
        {
            using var book = await EpubReader.OpenBookAsync(deviceOpenablePath, LenientBookReaderOptions);
            var readingOrder = (await book.GetReadingOrderAsync())
                .Where(c => c.ContentType == EpubContentType.XHTML_1_1)
                .ToList();
            if (readingOrder.Count == 0) return null;

            var pageIndex = Math.Clamp(pageNum, 0, Math.Max(readingOrder.Count - 1, 0));
            var preferred = readingOrder[pageIndex];
            var location = await TryResolveLocationInContentAsync(preferred, bookScrollId, ct);
            if (location != null) return location;

            foreach (var content in readingOrder)
            {
                ct.ThrowIfCancellationRequested();
                if (ReferenceEquals(content, preferred)) continue;
                location = await TryResolveLocationInContentAsync(content, bookScrollId, ct);
                if (location != null) return location;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Kobo BookScrollId→Location map failed for {Path}", deviceOpenablePath);
        }

        return null;
    }

    /// <summary>
    /// Finds an element for <paramref name="bookScrollId"/> and returns a Location only when a
    /// real <c>kobo.N.M</c> id exists on that element or an ancestor (never invents spans).
    /// </summary>
    internal static KoboMappedLocation? MapHtmlToLocation(HtmlDocument doc, string bookScrollId,
        string sourcePath)
    {
        var node = FindElementByXPath(doc, bookScrollId);
        if (node == null) return null;

        var spanId = FindKoboSpanId(node);
        if (spanId == null) return null;

        return new KoboMappedLocation(spanId, TypeKoboSpan, sourcePath);
    }

    /// <summary>
    /// When <paramref name="locationValue"/> is present as an element id, returns a descoped
    /// <c>id("…")</c> BookScrollId.
    /// </summary>
    internal static string? MapHtmlToBookScrollId(HtmlDocument doc, string locationValue)
    {
        if (string.IsNullOrWhiteSpace(locationValue)) return null;
        var node = doc.GetElementbyId(locationValue);
        if (node == null) return null;
        return $"id(\"{locationValue}\")";
    }

    private static async Task<string?> TryResolveBookScrollIdInContentAsync(
        EpubLocalTextContentFileRef content, string locationValue, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var html = await content.ReadContentAsync();
        var doc = new HtmlDocument { OptionFixNestedTags = true };
        doc.LoadHtml(html);
        return MapHtmlToBookScrollId(doc, locationValue);
    }

    private static async Task<KoboMappedLocation?> TryResolveLocationInContentAsync(
        EpubLocalTextContentFileRef content, string bookScrollId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var html = await content.ReadContentAsync();
        var doc = new HtmlDocument { OptionFixNestedTags = true };
        doc.LoadHtml(html);
        var source = !string.IsNullOrWhiteSpace(content.FilePath) ? content.FilePath : content.Key;
        return MapHtmlToLocation(doc, bookScrollId, source);
    }

    private static async Task<EpubLocalTextContentFileRef?> FindContentForSourceAsync(
        IReadOnlyList<EpubLocalTextContentFileRef> readingOrder, string? locationSource,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(locationSource)) return null;

        var normalized = NormalizePackagePath(locationSource);
        foreach (var content in readingOrder)
        {
            ct.ThrowIfCancellationRequested();
            if (PathsMatch(normalized, content.FilePath) || PathsMatch(normalized, content.Key))
            {
                return content;
            }
        }

        return null;
    }

    private static bool PathsMatch(string normalizedSource, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return false;
        var normalizedCandidate = NormalizePackagePath(candidate);
        if (string.Equals(normalizedSource, normalizedCandidate, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Allow suffix match (OEBPS/chapter.xhtml vs chapter.xhtml).
        return normalizedCandidate.EndsWith(normalizedSource, StringComparison.OrdinalIgnoreCase)
               || normalizedSource.EndsWith(normalizedCandidate, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePackagePath(string path)
    {
        var cleaned = BookService.CleanContentKeys(path.Replace('\\', '/').Trim());
        while (cleaned.StartsWith("./", StringComparison.Ordinal)) cleaned = cleaned[2..];
        return cleaned;
    }

    private static HtmlNode? FindElementByXPath(HtmlDocument doc, string xpath)
    {
        var idMatch = IdXPathRegex().Match(xpath.Trim());
        if (idMatch.Success)
        {
            var id = idMatch.Groups[1].Value;
            return string.IsNullOrWhiteSpace(id) ? null : doc.GetElementbyId(id);
        }

        try
        {
            return doc.DocumentNode.SelectSingleNode(xpath)
                   ?? doc.DocumentNode.SelectSingleNode(xpath.ToLowerInvariant());
        }
        catch
        {
            return null;
        }
    }

    private static string? FindKoboSpanId(HtmlNode node)
    {
        for (var current = node; current != null; current = current.ParentNode)
        {
            var id = current.GetAttributeValue("id", null);
            if (!string.IsNullOrWhiteSpace(id) && KoboSpanIdRegex().IsMatch(id))
            {
                return id;
            }
        }

        return null;
    }
}
