using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Kavita.API.Services;
using Kavita.Models.Entities;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Kobo;

/// <summary>
/// Owns the on-disk layout and lifecycle of the shared Kobo conversion cache:
/// fingerprint-keyed paths, pool classification, page-count validation, LRU pool caps,
/// and stale-fingerprint sweeps. <see cref="KoboConversionService"/> composes one of these.
/// </summary>
internal sealed class KoboConversionCacheStore(IDirectoryService directoryService, ILogger logger)
{
    /// <summary>
    /// Cache fingerprint over the source identity plus the structural contract version, so a contract
    /// bump orphans old artifacts. Shared by the EPUB and KEPUB pools for a chapter.
    /// </summary>
    public static string ComputeFingerprint(MangaFile file)
    {
        var raw =
            $"{KoboConversionService.ConvertContractVersion}|{file.FilePath}|{file.Bytes}|{file.LastModifiedUtc.Ticks}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public string GetDefaultCacheRoot() =>
        Path.Combine(directoryService.LongTermCacheDirectory, KoboConversionService.CacheFolderName);

    public string GetCacheDirectory(string cacheRoot, int chapterId) =>
        Path.Combine(cacheRoot, chapterId.ToString());

    public string GetCacheFilePath(string cacheRoot, int chapterId, string fingerprint) =>
        Path.Combine(GetCacheDirectory(cacheRoot, chapterId), $"{fingerprint}.epub");

    public string GetKepubCacheFilePath(string cacheRoot, int chapterId, string fingerprint) =>
        Path.Combine(GetCacheDirectory(cacheRoot, chapterId),
            $"{fingerprint}{KoboConversionService.KepubCacheExtension}");

    /// <summary>Convenience for callers/tests that use the default/stubbed long-term cache root.</summary>
    public string GetCacheDirectory(int chapterId) =>
        GetCacheDirectory(GetDefaultCacheRoot(), chapterId);

    public string GetCacheFilePath(int chapterId, string fingerprint) =>
        GetCacheFilePath(GetDefaultCacheRoot(), chapterId, fingerprint);

    public string GetKepubCacheFilePath(int chapterId, string fingerprint) =>
        GetKepubCacheFilePath(GetDefaultCacheRoot(), chapterId, fingerprint);

    public static bool IsEpubPoolFile(string path) =>
        path.EndsWith(".epub", StringComparison.OrdinalIgnoreCase) &&
        !path.EndsWith(KoboConversionService.KepubCacheExtension, StringComparison.OrdinalIgnoreCase) &&
        !path.EndsWith(".partial", StringComparison.OrdinalIgnoreCase);

    public static bool IsKepubPoolFile(string path) =>
        path.EndsWith(KoboConversionService.KepubCacheExtension, StringComparison.OrdinalIgnoreCase) &&
        !path.EndsWith(".partial", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Single source of truth for archive→EPUB page-count trust. Returns true when the spine matches
    /// <paramref name="expectedPages"/> (or when the check does not apply). On mismatch invokes
    /// <paramref name="onMismatch"/> with the observed spine count (null when unreadable) and returns false.
    /// </summary>
    public static bool ValidateSpinePageCount(string epubPath, int? expectedPages, Action<int?> onMismatch)
    {
        if (expectedPages is not > 0) return true;

        var spinePages = KoboConvertEpubInspector.TryCountSpinePages(epubPath);
        if (spinePages != null && spinePages.Value == expectedPages.Value) return true;

        onMismatch(spinePages);
        return false;
    }

    /// <summary>
    /// Read-path guard: returns the touched path when the cached artifact exists and (for archive converts)
    /// its spine matches <paramref name="expectedPages"/>; otherwise invalidates the mismatched file and returns null.
    /// </summary>
    public string? TouchIfValidCache(string path, int chapterId, int? expectedPages, string poolLabel)
    {
        if (!File.Exists(path)) return null;

        var valid = ValidateSpinePageCount(path, expectedPages, spinePages =>
        {
            logger.LogError(
                "Kobo {Pool} cache page-count mismatch for chapter {ChapterId}: spine={SpinePages}, chapter.Pages={ChapterPages}. Invalidating {Path}",
                poolLabel, chapterId, spinePages, expectedPages!.Value, path);
            try { File.Delete(path); }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Could not delete mismatched Kobo cache file {Path}", path);
            }
        });

        return valid ? TouchIfExists(path) : null;
    }

    public static string? TouchIfExists(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            File.SetLastAccessTimeUtc(path, DateTime.UtcNow);
        }
        catch
        {
            // Access-time updates are best-effort for LRU ordering.
        }

        return path;
    }

    /// <summary>Drops other-fingerprint artifacts in the same pool for this chapter (source changed).</summary>
    public void DeleteStaleFingerprints(string cacheDir, string keepPath, bool isKepubPool)
    {
        var pattern = isKepubPool ? "*" + KoboConversionService.KepubCacheExtension : "*.epub";
        Func<string, bool> isPoolFile = isKepubPool ? IsKepubPoolFile : IsEpubPoolFile;

        foreach (var stale in Directory.EnumerateFiles(cacheDir, pattern)
                     .Where(isPoolFile)
                     .Where(p => !string.Equals(p, keepPath, StringComparison.OrdinalIgnoreCase)))
        {
            try { File.Delete(stale); }
            catch (Exception ex)
            {
                if (isKepubPool)
                {
                    logger.LogDebug(ex, "Could not delete stale Kobo KEPUB cache file {Path}", stale);
                }
                else
                {
                    logger.LogDebug(ex, "Could not delete stale Kobo cache file {Path}", stale);
                }
            }
        }
    }

    public void EnforceEpubCap(string cacheRoot, long? maxBytes, string? protectPath) =>
        EnforcePoolCap(cacheRoot, maxBytes, isKepubPool: false, protectPath);

    public void EnforceKepubCap(string cacheRoot, long? maxBytes, string? protectPath) =>
        EnforcePoolCap(cacheRoot, maxBytes, isKepubPool: true, protectPath);

    /// <summary>
    /// Evicts least-recently-accessed artifacts in one pool until under <paramref name="maxBytes"/>.
    /// Null/≤0 means unlimited. Never deletes <paramref name="protectPath"/> (just-written file).
    /// </summary>
    private void EnforcePoolCap(string cacheRoot, long? maxBytes, bool isKepubPool, string? protectPath)
    {
        if (maxBytes is null or <= 0) return;
        if (!Directory.Exists(cacheRoot)) return;

        Func<string, bool> isPoolFile = isKepubPool ? IsKepubPoolFile : IsEpubPoolFile;
        var files = new List<FileInfo>();
        foreach (var path in Directory.EnumerateFiles(cacheRoot, "*", SearchOption.AllDirectories))
        {
            if (!isPoolFile(path)) continue;
            try
            {
                var info = new FileInfo(path);
                if (info.Exists) files.Add(info);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Could not stat Kobo cache file {Path}", path);
            }
        }

        var total = files.Sum(f => f.Length);
        if (total <= maxBytes.Value) return;

        foreach (var file in files.OrderBy(f => f.LastAccessTimeUtc).ThenBy(f => f.FullName))
        {
            if (total <= maxBytes.Value) break;
            if (protectPath != null &&
                string.Equals(file.FullName, protectPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                var length = file.Length;
                file.Delete();
                total -= length;
                logger.LogInformation(
                    "Evicted Kobo {Pool} cache file {Path} ({Bytes} bytes) to enforce max {MaxBytes}",
                    isKepubPool ? "KEPUB" : "EPUB", file.FullName, length, maxBytes.Value);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Could not evict Kobo cache file {Path}", file.FullName);
            }
        }
    }
}
