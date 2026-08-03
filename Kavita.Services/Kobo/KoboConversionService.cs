using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Common;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Kobo;

/// <summary>
/// Shared fingerprint cache under <c>cache-long/kobo</c> with in-request budget and fail-fast in-flight gating.
/// Separate optional byte caps apply LRU eviction to archive→EPUB and EPUB→KEPUB pools.
/// </summary>
public class KoboConversionService(
    ILogger<KoboConversionService> logger,
    IDirectoryService directoryService,
    IKoboArchiveEpubConverter archiveEpubConverter,
    IKepubifyRunner kepubifyRunner,
    IKoboConversionJobScheduler jobScheduler,
    IUnitOfWork unitOfWork,
    IEventHub eventHub,
    IKoboLocationRematchService koboLocationRematchService)
    : IKoboConversionService
{
    public const string Name = "KoboConversionService";
    public const string CacheFolderName = "kobo";
    public const string KepubCacheExtension = ".kepub.epub";
    public const string ConvertUnavailableMessage = "kobo-convert-unavailable";
    public const string ConvertFailedMessage = "kobo-convert-failed";

    /// <summary>
    /// Monotonic version of the archive→EPUB structural contract
    /// (<see cref="KoboArchiveEpubConverter"/> / <see cref="KoboConvertLocationCodec"/>).
    /// Bump when paths, spine membership, or page DOM rules change so EPUB and KEPUB
    /// cache fingerprints miss and old artifacts are orphaned.
    /// </summary>
    public const int ConvertContractVersion = 1;

    /// <summary>Process-wide in-flight chapter converts (download + background).</summary>
    private static readonly ConcurrentDictionary<int, byte> InFlight = new();

    public string? TryGetCachedKepubPath(int chapterId, MangaFile sourceFile)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);
        var fingerprint = ComputeFingerprint(sourceFile);
        var path = GetKepubCacheFilePath(chapterId, fingerprint);
        return TouchIfExists(path);
    }

    public async Task EnqueueKepubifyIfNeededAsync(int chapterId, MangaFile sourceFile,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        if (!settings.EnableKepubConversion) return;
        if (string.IsNullOrWhiteSpace(settings.KepubifyPath)) return;
        if (TryGetCachedKepubPath(chapterId, sourceFile) != null) return;

        jobScheduler.EnqueueBackgroundConvert(chapterId);
    }

    public async Task<string> GetOrConvertEpubAsync(int chapterId, MangaFile archiveFile, string title,
        int budgetSeconds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(archiveFile);
        if (budgetSeconds < 1) budgetSeconds = 1;

        var fingerprint = ComputeFingerprint(archiveFile);
        var cached = TryGetCachedPath(chapterId, fingerprint);
        if (cached != null) return cached;

        if (!InFlight.TryAdd(chapterId, 0))
        {
            logger.LogWarning("Kobo convert already in flight for chapter {ChapterId}; failing download fast",
                chapterId);
            throw new KavitaException(ConvertUnavailableMessage);
        }

        var handedOffToBackground = false;
        try
        {
            using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            budgetCts.CancelAfter(TimeSpan.FromSeconds(budgetSeconds));

            try
            {
                return await ConvertAndCacheEpubAsync(chapterId, archiveFile, title, fingerprint,
                    budgetCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                handedOffToBackground = true;
                logger.LogWarning(
                    "Kobo in-request convert for chapter {ChapterId} exceeded {BudgetSeconds}s budget; enqueueing background convert",
                    chapterId, budgetSeconds);
                jobScheduler.EnqueueBackgroundConvert(chapterId);
                throw new KavitaException(ConvertUnavailableMessage);
            }
        }
        catch (KavitaException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Hard failure converting chapter {ChapterId} for Kobo", chapterId);
            throw new KavitaException(ConvertFailedMessage);
        }
        finally
        {
            if (!handedOffToBackground)
            {
                InFlight.TryRemove(chapterId, out _);
            }
        }
    }

    public async Task<string> GetOrConvertKepubAsync(int chapterId, MangaFile sourceFile, string title,
        int budgetSeconds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);
        if (budgetSeconds < 1) budgetSeconds = 1;

        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        if (!settings.EnableKepubConversion)
        {
            throw new KavitaException("kobo-format-unsupported");
        }

        if (string.IsNullOrWhiteSpace(settings.KepubifyPath))
        {
            throw new KavitaException(ConvertFailedMessage);
        }

        var cached = TryGetCachedKepubPath(chapterId, sourceFile);
        if (cached != null) return cached;

        if (!InFlight.TryAdd(chapterId, 0))
        {
            logger.LogWarning("Kobo convert already in flight for chapter {ChapterId}; failing KEPUB download fast",
                chapterId);
            throw new KavitaException(ConvertUnavailableMessage);
        }

        var handedOffToBackground = false;
        try
        {
            using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            budgetCts.CancelAfter(TimeSpan.FromSeconds(budgetSeconds));

            try
            {
                return await ConvertAndCacheKepubAsync(chapterId, sourceFile, title, settings.KepubifyPath,
                    budgetCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                handedOffToBackground = true;
                logger.LogWarning(
                    "Kobo in-request KEPUB convert for chapter {ChapterId} exceeded {BudgetSeconds}s budget; enqueueing background convert",
                    chapterId, budgetSeconds);
                jobScheduler.EnqueueBackgroundConvert(chapterId);
                throw new KavitaException(ConvertUnavailableMessage);
            }
        }
        catch (KavitaException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Hard failure converting chapter {ChapterId} to KEPUB", chapterId);
            throw new KavitaException(ConvertFailedMessage);
        }
        finally
        {
            if (!handedOffToBackground)
            {
                InFlight.TryRemove(chapterId, out _);
            }
        }
    }

    public async Task ConvertChapterInBackgroundAsync(int chapterId, CancellationToken ct = default)
    {
        // May already be marked in-flight by a timed-out download request.
        InFlight.TryAdd(chapterId, 0);
        try
        {
            var chapter = await unitOfWork.DataContext.Chapter
                .Include(c => c.Files)
                .Include(c => c.Volume).ThenInclude(v => v.Series)
                .AsSplitQuery()
                .FirstOrDefaultAsync(c => c.Id == chapterId, ct);
            if (chapter == null)
            {
                logger.LogWarning("Background Kobo convert: chapter {ChapterId} not found", chapterId);
                return;
            }

            await ConvertChapterIfNeededAsync(chapter, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Background Kobo convert failed for chapter {ChapterId}", chapterId);
        }
        finally
        {
            InFlight.TryRemove(chapterId, out _);
        }
    }

    public async Task ConvertLibraryForKoboAsync(int libraryId, CancellationToken ct = default)
    {
        var library = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId, ct: ct);
        if (library == null)
        {
            logger.LogWarning("Kobo library convert: library {LibraryId} not found", libraryId);
            return;
        }

        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        var kepubEnabled = settings.EnableKepubConversion &&
                           !string.IsNullOrWhiteSpace(settings.KepubifyPath);

        logger.LogInformation(
            "[KoboConversionService] Beginning whole-library Kobo convert for {LibraryName} (kepub={KepubEnabled}). This can grow disk use under cache-long/kobo.",
            library.Name, kepubEnabled);

        var chapters = await unitOfWork.DataContext.Chapter
            .Include(c => c.Files)
            .Include(c => c.Volume).ThenInclude(v => v.Series)
            .Where(c => c.Volume.Series.LibraryId == libraryId)
            .AsSplitQuery()
            .ToListAsync(ct);

        var convertible = chapters
            .Where(c => NeedsLibraryWarmup(c, kepubEnabled))
            .ToList();

        var total = convertible.Count;
        logger.LogInformation(
            "[KoboConversionService] Library {LibraryName}: {ConvertibleCount} chapter(s) to convert",
            library.Name, total);

        await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.KoboConvertProgressEvent(library.Id, 0F, ProgressEventType.Started,
                $"Starting {library.Name}"), ct: ct);

        var index = 0;
        foreach (var chapter in convertible)
        {
            ct.ThrowIfCancellationRequested();

            var progress = total == 0 ? 1F : Math.Max(0F, Math.Min(1F, index * 1F / total));
            var subtitle = chapter.Volume?.Series?.Name ?? $"Chapter {chapter.Id}";
            await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.KoboConvertProgressEvent(library.Id, progress, ProgressEventType.Updated, subtitle),
                ct: ct);

            // Library warm-up is not bound by the in-request download time budget.
            InFlight.TryAdd(chapter.Id, 0);
            try
            {
                await ConvertChapterIfNeededAsync(chapter, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "[KoboConversionService] Failed converting chapter {ChapterId} during library {LibraryId} warm-up",
                    chapter.Id, libraryId);
            }
            finally
            {
                InFlight.TryRemove(chapter.Id, out _);
            }

            index++;
        }

        await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.KoboConvertProgressEvent(library.Id, 1F, ProgressEventType.Ended, "Complete"), ct: ct);

        logger.LogInformation(
            "[KoboConversionService] Finished whole-library Kobo convert for {LibraryName}: {Converted}/{Total}",
            library.Name, index, total);
    }

    public Task ClearConversionCacheAsync(CancellationToken ct = default)
    {
        var path = Path.Combine(directoryService.LongTermCacheDirectory, CacheFolderName);
        logger.LogInformation("Clearing Kobo conversion cache at {Path}", path);
        directoryService.ExistOrCreate(path);

        try
        {
            directoryService.ClearDirectory(path);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an issue clearing the Kobo conversion cache");
        }

        logger.LogInformation("Kobo conversion cache cleared");
        return Task.CompletedTask;
    }

    public async Task EnforceConversionCacheCapsAsync(CancellationToken ct = default)
    {
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        EnforcePoolCap(settings.KoboEpubCacheMaxBytes, isKepubPool: false, protectPath: null);
        ct.ThrowIfCancellationRequested();
        EnforcePoolCap(settings.KoboKepubCacheMaxBytes, isKepubPool: true, protectPath: null);
    }

    /// <summary>Test seam: clear process-wide in-flight markers between tests.</summary>
    internal static void ResetInFlightForTests() => InFlight.Clear();

    internal static string ComputeFingerprint(MangaFile file)
    {
        var raw =
            $"{ConvertContractVersion}|{file.FilePath}|{file.Bytes}|{file.LastModifiedUtc.Ticks}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    internal string GetCacheDirectory(int chapterId) =>
        Path.Combine(directoryService.LongTermCacheDirectory, CacheFolderName, chapterId.ToString());

    internal string GetCacheFilePath(int chapterId, string fingerprint) =>
        Path.Combine(GetCacheDirectory(chapterId), $"{fingerprint}.epub");

    internal string GetKepubCacheFilePath(int chapterId, string fingerprint) =>
        Path.Combine(GetCacheDirectory(chapterId), $"{fingerprint}{KepubCacheExtension}");

    internal static bool IsEpubPoolFile(string path) =>
        path.EndsWith(".epub", StringComparison.OrdinalIgnoreCase) &&
        !path.EndsWith(KepubCacheExtension, StringComparison.OrdinalIgnoreCase) &&
        !path.EndsWith(".partial", StringComparison.OrdinalIgnoreCase);

    internal static bool IsKepubPoolFile(string path) =>
        path.EndsWith(KepubCacheExtension, StringComparison.OrdinalIgnoreCase) &&
        !path.EndsWith(".partial", StringComparison.OrdinalIgnoreCase);

    private static bool NeedsLibraryWarmup(Chapter chapter, bool kepubEnabled)
    {
        var nativeEpub = KoboService.PreferNativeEpub(chapter.Files);
        var archive = KoboService.PreferConvertibleArchive(chapter.Files);
        if (nativeEpub == null && archive == null) return false;

        // Always warm archive→EPUB for CBZ/CBR without a native EPUB.
        if (nativeEpub == null && archive != null) return true;

        // Native EPUB chapters are only warmed when KEPUB production is enabled.
        return kepubEnabled && nativeEpub != null;
    }

    private async Task ConvertChapterIfNeededAsync(Chapter chapter, CancellationToken ct)
    {
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        var nativeEpub = KoboService.PreferNativeEpub(chapter.Files);
        var archive = KoboService.PreferConvertibleArchive(chapter.Files);
        var source = nativeEpub ?? archive;
        if (source == null)
        {
            logger.LogWarning("Background Kobo convert: chapter {ChapterId} has no EPUB/CBZ/CBR source",
                chapter.Id);
            return;
        }

        var title = chapter.Volume?.Series == null
            ? source.FileName
            : KoboService.BuildTitle(chapter.Volume.Series, chapter);

        // Archive → EPUB when there is no native EPUB.
        if (nativeEpub == null && archive != null)
        {
            var fingerprint = ComputeFingerprint(archive);
            if (TryGetCachedPath(chapter.Id, fingerprint) == null)
            {
                await ConvertAndCacheEpubAsync(chapter.Id, archive, title, fingerprint, ct);
            }
        }

        if (!settings.EnableKepubConversion) return;
        if (string.IsNullOrWhiteSpace(settings.KepubifyPath))
        {
            logger.LogWarning(
                "Background Kobo KEPUB convert skipped for chapter {ChapterId}: kepubify path is empty",
                chapter.Id);
            return;
        }

        if (TryGetCachedKepubPath(chapter.Id, source) != null) return;

        await ConvertAndCacheKepubAsync(chapter.Id, source, title, settings.KepubifyPath, ct);
    }

    private string? TryGetCachedPath(int chapterId, string fingerprint)
    {
        var path = GetCacheFilePath(chapterId, fingerprint);
        return TouchIfExists(path);
    }

    private static string? TouchIfExists(string path)
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

    private async Task<string> ResolveEpubInputPathAsync(int chapterId, MangaFile sourceFile, string title,
        CancellationToken ct)
    {
        if (sourceFile.Format == MangaFormat.Epub)
        {
            return sourceFile.FilePath;
        }

        var fingerprint = ComputeFingerprint(sourceFile);
        var cached = TryGetCachedPath(chapterId, fingerprint);
        if (cached != null) return cached;

        return await ConvertAndCacheEpubAsync(chapterId, sourceFile, title, fingerprint, ct);
    }

    private async Task<string> ConvertAndCacheEpubAsync(int chapterId, MangaFile archiveFile, string title,
        string fingerprint, CancellationToken ct)
    {
        var cacheDir = GetCacheDirectory(chapterId);
        directoryService.ExistOrCreate(cacheDir);

        var finalPath = GetCacheFilePath(chapterId, fingerprint);
        var tempPath = finalPath + ".partial";

        try
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            await archiveEpubConverter.ConvertAsync(archiveFile.FilePath, tempPath, title, ct);
            ct.ThrowIfCancellationRequested();

            if (File.Exists(finalPath)) File.Delete(finalPath);
            File.Move(tempPath, finalPath);
            TouchIfExists(finalPath);

            // Drop stale fingerprints for this chapter (source changed). Keep KEPUB artifacts.
            foreach (var stale in Directory.EnumerateFiles(cacheDir, "*.epub")
                         .Where(IsEpubPoolFile)
                         .Where(p => !string.Equals(p, finalPath, StringComparison.OrdinalIgnoreCase)))
            {
                try { File.Delete(stale); }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Could not delete stale Kobo cache file {Path}", stale);
                }
            }

            await EnforceEpubCapAfterWriteAsync(finalPath, ct);
            return finalPath;
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
            catch
            {
                // ignore cleanup races
            }
        }
    }

    private async Task<string> ConvertAndCacheKepubAsync(int chapterId, MangaFile sourceFile, string title,
        string kepubifyPath, CancellationToken ct)
    {
        var fingerprint = ComputeFingerprint(sourceFile);
        var existing = GetKepubCacheFilePath(chapterId, fingerprint);
        if (File.Exists(existing))
        {
            TouchIfExists(existing);
            return existing;
        }

        var epubInput = await ResolveEpubInputPathAsync(chapterId, sourceFile, title, ct);
        ct.ThrowIfCancellationRequested();

        var cacheDir = GetCacheDirectory(chapterId);
        directoryService.ExistOrCreate(cacheDir);

        var finalPath = GetKepubCacheFilePath(chapterId, fingerprint);
        var tempPath = finalPath + ".partial";

        try
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            await kepubifyRunner.ConvertAsync(kepubifyPath, epubInput, tempPath, ct);
            ct.ThrowIfCancellationRequested();

            if (File.Exists(finalPath)) File.Delete(finalPath);
            File.Move(tempPath, finalPath);
            TouchIfExists(finalPath);

            // Drop stale KEPUB fingerprints for this chapter (source changed).
            foreach (var stale in Directory.EnumerateFiles(cacheDir, "*" + KepubCacheExtension)
                         .Where(IsKepubPoolFile)
                         .Where(p => !string.Equals(p, finalPath, StringComparison.OrdinalIgnoreCase)))
            {
                try { File.Delete(stale); }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Could not delete stale Kobo KEPUB cache file {Path}", stale);
                }
            }

            await DropSyncedSetForChapterAsync(chapterId, ct);
            await koboLocationRematchService.RematchAfterDeviceFileChangeAsync(chapterId, finalPath, ct);
            await EnforceKepubCapAfterWriteAsync(finalPath, ct);
            return finalPath;
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
            catch
            {
                // ignore cleanup races
            }
        }
    }

    private async Task EnforceEpubCapAfterWriteAsync(string justWrittenPath, CancellationToken ct)
    {
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        EnforcePoolCap(settings.KoboEpubCacheMaxBytes, isKepubPool: false, protectPath: justWrittenPath);
    }

    private async Task EnforceKepubCapAfterWriteAsync(string justWrittenPath, CancellationToken ct)
    {
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        EnforcePoolCap(settings.KoboKepubCacheMaxBytes, isKepubPool: true, protectPath: justWrittenPath);
    }

    /// <summary>
    /// Evicts least-recently-accessed artifacts in one pool until under <paramref name="maxBytes"/>.
    /// Null/≤0 means unlimited. Never deletes <paramref name="protectPath"/> (just-written file).
    /// </summary>
    private void EnforcePoolCap(long? maxBytes, bool isKepubPool, string? protectPath)
    {
        if (maxBytes is null or <= 0) return;

        var root = Path.Combine(directoryService.LongTermCacheDirectory, CacheFolderName);
        if (!Directory.Exists(root)) return;

        Func<string, bool> isPoolFile = isKepubPool ? IsKepubPoolFile : IsEpubPoolFile;
        var files = new List<FileInfo>();
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
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

    /// <summary>
    /// Clears synced-set rows for every user that has this chapter, so the next sync re-offers KEPUB-only URLs.
    /// </summary>
    private async Task DropSyncedSetForChapterAsync(int chapterId, CancellationToken ct)
    {
        var rows = await unitOfWork.DataContext.AppUserKoboSyncedChapter
            .Where(s => s.ChapterId == chapterId)
            .ToListAsync(ct);
        if (rows.Count == 0) return;

        unitOfWork.DataContext.AppUserKoboSyncedChapter.RemoveRange(rows);
        await unitOfWork.CommitAsync(ct);
        logger.LogInformation(
            "Dropped {Count} Kobo synced-set row(s) for chapter {ChapterId} after KEPUB cache write",
            rows.Count, chapterId);
    }
}
