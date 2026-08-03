using System;
using System.Collections.Concurrent;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Kobo;

/// <summary>
/// Shared fingerprint cache under <c>cache-long/kobo</c> with in-request budget and fail-fast in-flight gating.
/// </summary>
public class KoboConversionService(
    ILogger<KoboConversionService> logger,
    IDirectoryService directoryService,
    IKoboArchiveEpubConverter archiveEpubConverter,
    IKoboConversionJobScheduler jobScheduler,
    IUnitOfWork unitOfWork,
    IEventHub eventHub)
    : IKoboConversionService
{
    public const string Name = "KoboConversionService";
    public const string CacheFolderName = "kobo";
    public const string KepubCacheExtension = ".kepub.epub";
    public const string ConvertUnavailableMessage = "kobo-convert-unavailable";
    public const string ConvertFailedMessage = "kobo-convert-failed";

    /// <summary>Process-wide in-flight chapter converts (download + background).</summary>
    private static readonly ConcurrentDictionary<int, byte> InFlight = new();

    public string? TryGetCachedKepubPath(int chapterId, MangaFile sourceFile)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);
        var fingerprint = ComputeFingerprint(sourceFile);
        var path = GetKepubCacheFilePath(chapterId, fingerprint);
        return File.Exists(path) ? path : null;
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
                return await ConvertAndCacheAsync(chapterId, archiveFile, title, fingerprint, budgetCts.Token);
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

        logger.LogInformation(
            "[KoboConversionService] Beginning whole-library Kobo convert for {LibraryName}. This can grow disk use under cache-long/kobo.",
            library.Name);

        var chapters = await unitOfWork.DataContext.Chapter
            .Include(c => c.Files)
            .Include(c => c.Volume).ThenInclude(v => v.Series)
            .Where(c => c.Volume.Series.LibraryId == libraryId)
            .AsSplitQuery()
            .ToListAsync(ct);

        var convertible = chapters
            .Where(c => KoboService.PreferNativeEpub(c.Files) == null
                        && KoboService.PreferConvertibleArchive(c.Files) != null)
            .ToList();

        var total = convertible.Count;
        logger.LogInformation(
            "[KoboConversionService] Library {LibraryName}: {ConvertibleCount} CBZ/CBR chapter(s) to convert",
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

    /// <summary>Test seam: clear process-wide in-flight markers between tests.</summary>
    internal static void ResetInFlightForTests() => InFlight.Clear();

    internal static string ComputeFingerprint(MangaFile file)
    {
        var raw = $"{file.FilePath}|{file.Bytes}|{file.LastModifiedUtc.Ticks}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    internal string GetCacheDirectory(int chapterId) =>
        Path.Combine(directoryService.LongTermCacheDirectory, CacheFolderName, chapterId.ToString());

    internal string GetCacheFilePath(int chapterId, string fingerprint) =>
        Path.Combine(GetCacheDirectory(chapterId), $"{fingerprint}.epub");

    internal string GetKepubCacheFilePath(int chapterId, string fingerprint) =>
        Path.Combine(GetCacheDirectory(chapterId), $"{fingerprint}{KepubCacheExtension}");

    private async Task ConvertChapterIfNeededAsync(Chapter chapter, CancellationToken ct)
    {
        var archive = KoboService.PreferConvertibleArchive(chapter.Files);
        if (archive == null)
        {
            logger.LogWarning("Background Kobo convert: chapter {ChapterId} has no CBZ/CBR source",
                chapter.Id);
            return;
        }

        if (KoboService.PreferNativeEpub(chapter.Files) != null)
        {
            logger.LogDebug("Background Kobo convert skipped for chapter {ChapterId}: native EPUB present",
                chapter.Id);
            return;
        }

        var fingerprint = ComputeFingerprint(archive);
        if (TryGetCachedPath(chapter.Id, fingerprint) != null) return;

        var title = chapter.Volume?.Series == null
            ? archive.FileName
            : KoboService.BuildTitle(chapter.Volume.Series, chapter);

        await ConvertAndCacheAsync(chapter.Id, archive, title, fingerprint, ct);
    }

    private string? TryGetCachedPath(int chapterId, string fingerprint)
    {
        var path = GetCacheFilePath(chapterId, fingerprint);
        return File.Exists(path) ? path : null;
    }

    private async Task<string> ConvertAndCacheAsync(int chapterId, MangaFile archiveFile, string title,
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

            // Drop stale fingerprints for this chapter (source changed). Keep KEPUB artifacts.
            foreach (var stale in Directory.EnumerateFiles(cacheDir, "*.epub")
                         .Where(p => !p.EndsWith(KepubCacheExtension, StringComparison.OrdinalIgnoreCase))
                         .Where(p => !string.Equals(p, finalPath, StringComparison.OrdinalIgnoreCase)))
            {
                try { File.Delete(stale); }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Could not delete stale Kobo cache file {Path}", stale);
                }
            }

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
}
