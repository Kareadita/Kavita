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
using Kavita.Common;
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
    IUnitOfWork unitOfWork)
    : IKoboConversionService
{
    public const string CacheFolderName = "kobo";
    public const string ConvertUnavailableMessage = "kobo-convert-unavailable";
    public const string ConvertFailedMessage = "kobo-convert-failed";

    /// <summary>Process-wide in-flight chapter converts (download + background).</summary>
    private static readonly ConcurrentDictionary<int, byte> InFlight = new();

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

            var archive = KoboService.PreferConvertibleArchive(chapter.Files);
            if (archive == null)
            {
                logger.LogWarning("Background Kobo convert: chapter {ChapterId} has no CBZ/CBR source",
                    chapterId);
                return;
            }

            if (KoboService.PreferNativeEpub(chapter.Files) != null)
            {
                logger.LogDebug("Background Kobo convert skipped for chapter {ChapterId}: native EPUB present",
                    chapterId);
                return;
            }

            var fingerprint = ComputeFingerprint(archive);
            if (TryGetCachedPath(chapterId, fingerprint) != null) return;

            var title = chapter.Volume?.Series == null
                ? archive.FileName
                : KoboService.BuildTitle(chapter.Volume.Series, chapter);

            await ConvertAndCacheAsync(chapterId, archive, title, fingerprint, ct);
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

            // Drop stale fingerprints for this chapter (source changed).
            foreach (var stale in Directory.EnumerateFiles(cacheDir, "*.epub")
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
