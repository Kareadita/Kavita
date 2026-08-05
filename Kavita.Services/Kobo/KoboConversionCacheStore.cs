using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Kavita.API.Services;
using Kavita.Models.Entities;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Kobo;

/// <summary>
/// Library + series + chapter identity used to build nested conversion-cache paths.
/// </summary>
internal readonly record struct KoboCacheIdentity(
    int LibraryId, string LibraryName, int SeriesId, string SeriesName, int ChapterId);

/// <summary>
/// Owns the on-disk layout and lifecycle of the shared Kobo conversion cache:
/// fingerprint-keyed paths, pool classification, page-count validation, LRU pool caps,
/// and stale-fingerprint sweeps. <see cref="KoboConversionService"/> composes one of these.
/// </summary>
internal sealed class KoboConversionCacheStore(IDirectoryService directoryService, ILogger logger)
{
    private const int MaxSanitizedNameLength = 100;
    private static readonly Regex CollapsedWhitespace = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Characters unsafe in folder names on Windows (and awkward elsewhere). Always stripped so
    /// cache paths stay portable regardless of the host OS's <see cref="Path.GetInvalidFileNameChars"/>.
    /// </summary>
    private static readonly char[] CrossPlatformInvalidNameChars =
        ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

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

    /// <summary>
    /// Formats a recognizable folder segment as <c>{id} - {sanitizedName}</c>, or just <c>{id}</c>
    /// when the name sanitizes to empty.
    /// </summary>
    public static string FormatIdNameFolder(int id, string? name)
    {
        var sanitized = SanitizeFolderName(name);
        return string.IsNullOrEmpty(sanitized) ? id.ToString() : $"{id} - {sanitized}";
    }

    public static string SanitizeFolderName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        var invalid = Path.GetInvalidFileNameChars();
        var buffer = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsControl(c) ||
                Array.IndexOf(invalid, c) >= 0 ||
                Array.IndexOf(CrossPlatformInvalidNameChars, c) >= 0)
            {
                buffer.Append(' ');
            }
            else
            {
                buffer.Append(c);
            }
        }

        var collapsed = CollapsedWhitespace.Replace(buffer.ToString(), " ").Trim().Trim('.');
        if (collapsed.Length > MaxSanitizedNameLength)
        {
            collapsed = collapsed[..MaxSanitizedNameLength].Trim().Trim('.');
        }

        return collapsed;
    }

    public string GetDefaultCacheRoot() =>
        Path.Combine(directoryService.LongTermCacheDirectory, KoboConversionService.CacheFolderName);

    public string GetCacheDirectory(string cacheRoot, KoboCacheIdentity identity) =>
        Path.Combine(cacheRoot,
            FormatIdNameFolder(identity.LibraryId, identity.LibraryName),
            FormatIdNameFolder(identity.SeriesId, identity.SeriesName),
            identity.ChapterId.ToString());

    /// <summary>Legacy flat layout used when series/library identity is unavailable.</summary>
    public string GetLegacyCacheDirectory(string cacheRoot, int chapterId) =>
        Path.Combine(cacheRoot, chapterId.ToString());

    public string GetCacheFilePath(string cacheRoot, KoboCacheIdentity identity, string fingerprint) =>
        Path.Combine(GetCacheDirectory(cacheRoot, identity), $"{fingerprint}.epub");

    public string GetKepubCacheFilePath(string cacheRoot, KoboCacheIdentity identity, string fingerprint) =>
        Path.Combine(GetCacheDirectory(cacheRoot, identity),
            $"{fingerprint}{KoboConversionService.KepubCacheExtension}");

    public string GetLegacyCacheFilePath(string cacheRoot, int chapterId, string fingerprint) =>
        Path.Combine(GetLegacyCacheDirectory(cacheRoot, chapterId), $"{fingerprint}.epub");

    public string GetLegacyKepubCacheFilePath(string cacheRoot, int chapterId, string fingerprint) =>
        Path.Combine(GetLegacyCacheDirectory(cacheRoot, chapterId),
            $"{fingerprint}{KoboConversionService.KepubCacheExtension}");

    /// <summary>
    /// Returns the preferred artifact path, migrating a legacy or renamed chapter directory into place
    /// when an older layout still holds the file.
    /// </summary>
    public string ResolveCacheFilePath(string cacheRoot, KoboCacheIdentity identity, string fingerprint,
        bool isKepub)
    {
        var preferredDir = GetCacheDirectory(cacheRoot, identity);
        var fileName = isKepub
            ? $"{fingerprint}{KoboConversionService.KepubCacheExtension}"
            : $"{fingerprint}.epub";
        var preferredPath = Path.Combine(preferredDir, fileName);

        if (File.Exists(preferredPath)) return preferredPath;

        var sourceFile = FindExistingArtifactFile(cacheRoot, identity, fileName, preferredPath);
        if (sourceFile == null) return preferredPath;

        var sourceChapterDir = Path.GetDirectoryName(sourceFile);
        if (sourceChapterDir != null &&
            !string.Equals(sourceChapterDir, preferredDir, StringComparison.OrdinalIgnoreCase))
        {
            MigrateChapterDirectory(cacheRoot, sourceChapterDir, preferredDir);
        }

        return preferredPath;
    }

    /// <summary>
    /// Locates an artifact file for <paramref name="identity"/> under preferred-adjacent, renamed,
    /// cross-library, or legacy layouts. Skips <paramref name="excludePath"/>.
    /// </summary>
    public string? FindExistingArtifactFile(string cacheRoot, KoboCacheIdentity identity, string fileName,
        string? excludePath = null)
    {
        var preferredLibraryDir = Path.Combine(cacheRoot,
            FormatIdNameFolder(identity.LibraryId, identity.LibraryName));
        var underPreferredLibrary = FindSeriesChapterArtifact(preferredLibraryDir, identity.SeriesId,
            identity.ChapterId, fileName, excludePath);
        if (underPreferredLibrary != null) return underPreferredLibrary;

        foreach (var libraryDir in EnumerateIdPrefixedDirectories(cacheRoot, identity.LibraryId))
        {
            var found = FindSeriesChapterArtifact(libraryDir, identity.SeriesId, identity.ChapterId,
                fileName, excludePath);
            if (found != null) return found;
        }

        // Series moved between libraries: scan other top-level folders (library count is small).
        if (Directory.Exists(cacheRoot))
        {
            foreach (var libraryDir in Directory.EnumerateDirectories(cacheRoot))
            {
                var name = Path.GetFileName(libraryDir);
                // Skip legacy flat chapter dirs at the cache root.
                if (int.TryParse(name, out _)) continue;

                var found = FindSeriesChapterArtifact(libraryDir, identity.SeriesId, identity.ChapterId,
                    fileName, excludePath);
                if (found != null) return found;
            }
        }

        var legacy = Path.Combine(GetLegacyCacheDirectory(cacheRoot, identity.ChapterId), fileName);
        if (File.Exists(legacy) &&
            (excludePath == null ||
             !string.Equals(legacy, excludePath, StringComparison.OrdinalIgnoreCase)))
        {
            return legacy;
        }

        return null;
    }

    private static string? FindSeriesChapterArtifact(string libraryDir, int seriesId, int chapterId,
        string fileName, string? excludePath)
    {
        if (!Directory.Exists(libraryDir)) return null;

        var chapterSegment = chapterId.ToString();
        foreach (var seriesDir in EnumerateIdPrefixedDirectories(libraryDir, seriesId))
        {
            var candidate = Path.Combine(seriesDir, chapterSegment, fileName);
            if (excludePath != null &&
                string.Equals(candidate, excludePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }

    private static IEnumerable<string> EnumerateIdPrefixedDirectories(string parent, int id)
    {
        if (!Directory.Exists(parent)) yield break;

        var prefix = $"{id} - ";
        var idOnly = id.ToString();
        foreach (var dir in Directory.EnumerateDirectories(parent))
        {
            var name = Path.GetFileName(dir);
            if (name.StartsWith(prefix, StringComparison.Ordinal) ||
                string.Equals(name, idOnly, StringComparison.Ordinal))
            {
                yield return dir;
            }
        }
    }

    private void MigrateChapterDirectory(string cacheRoot, string sourceDir, string destDir)
    {
        try
        {
            directoryService.ExistOrCreate(destDir);

            foreach (var file in Directory.EnumerateFiles(sourceDir))
            {
                var dest = Path.Combine(destDir, Path.GetFileName(file));
                try
                {
                    if (File.Exists(dest))
                    {
                        File.Delete(file);
                        continue;
                    }

                    File.Move(file, dest);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Could not migrate Kobo cache file {Source} to {Dest}", file, dest);
                }
            }

            TryDeleteEmptyAncestors(sourceDir, cacheRoot);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not migrate Kobo cache directory {Source} to {Dest}", sourceDir, destDir);
        }
    }

    private void TryDeleteEmptyAncestors(string startDir, string stopAtRoot)
    {
        var current = startDir;
        while (!string.IsNullOrEmpty(current) &&
               !string.Equals(current, stopAtRoot, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                if (!Directory.Exists(current) || Directory.EnumerateFileSystemEntries(current).Any())
                {
                    break;
                }

                var parent = Directory.GetParent(current)?.FullName;
                Directory.Delete(current);
                current = parent;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Could not delete empty Kobo cache directory {Path}", current);
                break;
            }
        }
    }

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
        if (!Directory.Exists(cacheDir)) return;

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
