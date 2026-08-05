using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Common;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Helpers;
using Kavita.Services.Scanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Kobo;

/// <summary>
/// Shared fingerprint cache under the configured Kobo conversion cache directory
/// (default <c>cache-long/kobo</c>) with in-request budget and fail-fast in-flight gating.
/// Separate optional byte caps apply LRU eviction to archive→EPUB and EPUB→KEPUB pools.
/// </summary>
public class KoboConversionService(
    ILogger<KoboConversionService> logger,
    IDirectoryService directoryService,
    IKoboArchiveEpubConverter archiveEpubConverter,
    IKepubifyRunner kepubifyRunner,
    IKepubifyPathResolver kepubifyPathResolver,
    IKoboConversionJobScheduler jobScheduler,
    IUnitOfWork unitOfWork,
    IEventHub eventHub,
    IKoboLocationRematchService koboLocationRematchService)
    : IKoboConversionService
{
    public const string Name = "KoboConversionService";
    public const string CacheFolderName = KoboSettingsDefaults.CacheFolderName;
    public const string KepubCacheExtension = ".kepub.epub";
    public const string ConvertUnavailableMessage = "kobo-convert-unavailable";
    public const string ConvertFailedMessage = "kobo-convert-failed";

    /// <summary>
    /// Monotonic version of the archive→EPUB structural contract
    /// (<see cref="KoboArchiveEpubConverter"/> / <see cref="KoboConvertLocationCodec"/>).
    /// Bump when paths, spine membership, or page DOM rules change so EPUB and KEPUB
    /// cache fingerprints miss and old artifacts are orphaned.
    /// </summary>
    public const int ConvertContractVersion = 2;

    /// <summary>Process-wide in-flight chapter converts (download + background).</summary>
    private static readonly ConcurrentDictionary<int, byte> InFlight = new();

    private readonly KoboConversionCacheStore _cacheStore = new(directoryService, logger);

    public async Task<string?> TryGetCachedKepubPathAsync(int chapterId, MangaFile sourceFile,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);
        var cacheRoot = await ResolveCacheRootAsync(ct);
        var fingerprint = KoboConversionCacheStore.ComputeFingerprint(sourceFile);
        var identity = await TryResolveCacheIdentityAsync(chapterId, ct);
        var path = ResolveArtifactPath(cacheRoot, identity, chapterId, fingerprint, isKepub: true);
        // Page-count trust applies to CBZ/CBR converts; native EPUB spines are not remapped.
        var expectedPages = sourceFile.Format == MangaFormat.Epub
            ? null
            : TryGetChapterPages(chapterId);
        return _cacheStore.TouchIfValidCache(path, chapterId, expectedPages, "KEPUB");
    }

    public async Task EnqueueKepubifyIfNeededAsync(int chapterId, MangaFile sourceFile,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        if (!settings.EnableKepubConversion) return;
        if (kepubifyPathResolver.Resolve(settings.KepubifyPath) == null) return;
        if (IsAlreadyKepubLibraryFile(sourceFile)) return;

        if (await TryGetCachedKepubPathAsync(chapterId, sourceFile, ct) != null)
        {
            EnqueuePromoteIfNeeded(chapterId, sourceFile, settings);
            return;
        }

        jobScheduler.EnqueueBackgroundConvert(chapterId);
    }

    public async Task<string> GetOrConvertEpubAsync(int chapterId, MangaFile archiveFile, string title,
        int budgetSeconds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(archiveFile);

        var cacheRoot = await ResolveCacheRootAsync(ct);
        var identity = await TryResolveCacheIdentityAsync(chapterId, ct);
        var expectedPages = await GetChapterPagesAsync(chapterId, ct);
        var fingerprint = KoboConversionCacheStore.ComputeFingerprint(archiveFile);
        var cached = TryGetCachedPath(cacheRoot, identity, chapterId, fingerprint, expectedPages);
        if (cached != null) return cached;

        return await GetOrConvertWithBudgetAsync(chapterId, budgetSeconds, isKepub: false,
            token => ConvertAndCacheEpubAsync(cacheRoot, identity, chapterId, archiveFile, title, fingerprint,
                expectedPages, token), ct);
    }

    public async Task<string> GetOrConvertKepubAsync(int chapterId, MangaFile sourceFile, string title,
        int budgetSeconds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);

        // Library file already promoted to KEPUB: serve it directly (no re-convert / no cache).
        if (IsAlreadyKepubLibraryFile(sourceFile))
        {
            return sourceFile.FilePath;
        }

        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        if (!settings.EnableKepubConversion)
        {
            throw new KavitaException("kobo-format-unsupported");
        }

        var kepubifyPath = kepubifyPathResolver.Resolve(settings.KepubifyPath);
        if (kepubifyPath == null)
        {
            throw new KavitaException(ConvertFailedMessage);
        }

        var cacheRoot = ResolveCacheRoot(settings);
        var identity = await TryResolveCacheIdentityAsync(chapterId, ct);
        var cached = await TryGetCachedKepubPathAsync(chapterId, sourceFile, ct);
        if (cached != null)
        {
            EnqueuePromoteIfNeeded(chapterId, sourceFile, settings);
            return cached;
        }

        return await GetOrConvertWithBudgetAsync(chapterId, budgetSeconds, isKepub: true,
            token => ConvertAndCacheKepubAsync(cacheRoot, identity, chapterId, sourceFile, title, kepubifyPath,
                token), ct);
    }

    /// <summary>
    /// Shared in-request skeleton for the EPUB and KEPUB download paths: fail-fast in-flight gate,
    /// per-request time budget, background hand-off on budget exhaustion, and error normalization.
    /// </summary>
    private async Task<string> GetOrConvertWithBudgetAsync(int chapterId, int budgetSeconds, bool isKepub,
        Func<CancellationToken, Task<string>> convert, CancellationToken ct)
    {
        if (budgetSeconds < 1) budgetSeconds = 1;

        if (!InFlight.TryAdd(chapterId, 0))
        {
            if (isKepub)
            {
                logger.LogWarning(
                    "Kobo convert already in flight for chapter {ChapterId}; failing KEPUB download fast", chapterId);
            }
            else
            {
                logger.LogWarning(
                    "Kobo convert already in flight for chapter {ChapterId}; failing download fast", chapterId);
            }

            throw new KavitaException(ConvertUnavailableMessage);
        }

        var handedOffToBackground = false;
        try
        {
            using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            budgetCts.CancelAfter(TimeSpan.FromSeconds(budgetSeconds));

            try
            {
                return await convert(budgetCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                handedOffToBackground = true;
                if (isKepub)
                {
                    logger.LogWarning(
                        "Kobo in-request KEPUB convert for chapter {ChapterId} exceeded {BudgetSeconds}s budget; enqueueing background convert",
                        chapterId, budgetSeconds);
                }
                else
                {
                    logger.LogWarning(
                        "Kobo in-request convert for chapter {ChapterId} exceeded {BudgetSeconds}s budget; enqueueing background convert",
                        chapterId, budgetSeconds);
                }

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
            if (isKepub)
            {
                logger.LogError(ex, "Hard failure converting chapter {ChapterId} to KEPUB", chapterId);
            }
            else
            {
                logger.LogError(ex, "Hard failure converting chapter {ChapterId} for Kobo", chapterId);
            }

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
        if (!InFlight.TryAdd(chapterId, 0))
        {
            logger.LogDebug("Background Kobo convert skipped; chapter {ChapterId} already in flight", chapterId);
            return;
        }

        try
        {
            var chapter = await unitOfWork.DataContext.Chapter
                .Include(c => c.Files)
                .Include(c => c.Volume).ThenInclude(v => v.Series).ThenInclude(s => s.Library)
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
                           kepubifyPathResolver.Resolve(settings.KepubifyPath) != null;
        var cacheRoot = ResolveCacheRoot(settings);

        logger.LogInformation(
            "[KoboConversionService] Beginning whole-library Kobo convert for {LibraryName} (kepub={KepubEnabled}). This can grow disk use under {CacheRoot}.",
            library.Name, kepubEnabled, cacheRoot);

        var convertible = await LoadConvertibleChaptersAsync(libraryId, kepubEnabled, ct);
        var total = convertible.Count;
        logger.LogInformation(
            "[KoboConversionService] Library {LibraryName}: {ConvertibleCount} chapter(s) to convert",
            library.Name, total);

        await ReportWarmupProgressAsync(library.Id, 0F, ProgressEventType.Started, $"Starting {library.Name}", ct);

        var index = 0;
        foreach (var chapter in convertible)
        {
            ct.ThrowIfCancellationRequested();

            var progress = total == 0 ? 1F : Math.Max(0F, Math.Min(1F, index * 1F / total));
            var subtitle = chapter.Volume?.Series?.Name ?? $"Chapter {chapter.Id}";
            await ReportWarmupProgressAsync(library.Id, progress, ProgressEventType.Updated, subtitle, ct);

            await WarmChapterAsync(chapter, libraryId, ct);
            index++;
        }

        await ReportWarmupProgressAsync(library.Id, 1F, ProgressEventType.Ended, "Complete", ct);

        logger.LogInformation(
            "[KoboConversionService] Finished whole-library Kobo convert for {LibraryName}: {Converted}/{Total}",
            library.Name, index, total);
    }

    private async Task<List<Chapter>> LoadConvertibleChaptersAsync(int libraryId, bool kepubEnabled,
        CancellationToken ct)
    {
        var chapters = await unitOfWork.DataContext.Chapter
            .Include(c => c.Files)
            .Include(c => c.Volume).ThenInclude(v => v.Series).ThenInclude(s => s.Library)
            .Where(c => c.Volume.Series.LibraryId == libraryId)
            .AsSplitQuery()
            .ToListAsync(ct);

        return chapters
            .Where(c => NeedsLibraryWarmup(c, kepubEnabled))
            .ToList();
    }

    private async Task WarmChapterAsync(Chapter chapter, int libraryId, CancellationToken ct)
    {
        // Library warm-up is not bound by the in-request download time budget.
        if (!InFlight.TryAdd(chapter.Id, 0))
        {
            logger.LogDebug("Background Kobo convert skipped; chapter {ChapterId} already in flight", chapter.Id);
            return;
        }

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
    }

    private Task ReportWarmupProgressAsync(int libraryId, float progress, string eventType,
        string subtitle, CancellationToken ct) =>
        eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.KoboConvertProgressEvent(libraryId, progress, eventType, subtitle), ct: ct);

    public async Task ClearConversionCacheAsync(CancellationToken ct = default)
    {
        var path = await ResolveCacheRootAsync(ct);
        logger.LogInformation("Clearing Kobo conversion cache at {Path}", path);
        directoryService.ExistOrCreate(path);

        try
        {
            directoryService.ClearDirectory(path);
            logger.LogInformation("Kobo conversion cache cleared");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an issue clearing the Kobo conversion cache");
        }
    }

    public async Task EnforceConversionCacheCapsAsync(CancellationToken ct = default)
    {
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        var cacheRoot = ResolveCacheRoot(settings);
        _cacheStore.EnforceEpubCap(cacheRoot, settings.KoboEpubCacheMaxBytes, protectPath: null);
        ct.ThrowIfCancellationRequested();
        _cacheStore.EnforceKepubCap(cacheRoot, settings.KoboKepubCacheMaxBytes, protectPath: null);
    }

    /// <summary>Test seam: clear process-wide in-flight markers between tests.</summary>
    internal static void ResetInFlightForTests() => InFlight.Clear();

    /// <summary>Test seam over <see cref="KoboConversionCacheStore.ComputeFingerprint"/>.</summary>
    internal static string ComputeFingerprint(MangaFile file) =>
        KoboConversionCacheStore.ComputeFingerprint(file);

    /// <summary>Test seam over <see cref="KoboConversionCacheStore.IsEpubPoolFile"/>.</summary>
    internal static bool IsEpubPoolFile(string path) => KoboConversionCacheStore.IsEpubPoolFile(path);

    /// <summary>Test seam: preferred nested EPUB cache path for a known library/series identity.</summary>
    internal string GetCacheFilePath(KoboCacheIdentity identity, string fingerprint) =>
        _cacheStore.GetCacheFilePath(_cacheStore.GetDefaultCacheRoot(), identity, fingerprint);

    /// <summary>Test seam: preferred nested KEPUB cache path for a known library/series identity.</summary>
    internal string GetKepubCacheFilePath(KoboCacheIdentity identity, string fingerprint) =>
        _cacheStore.GetKepubCacheFilePath(_cacheStore.GetDefaultCacheRoot(), identity, fingerprint);

    /// <summary>Test seam: legacy flat EPUB path when identity is unavailable.</summary>
    internal string GetLegacyCacheFilePath(int chapterId, string fingerprint) =>
        _cacheStore.GetLegacyCacheFilePath(_cacheStore.GetDefaultCacheRoot(), chapterId, fingerprint);

    /// <summary>Test seam: legacy flat KEPUB path when identity is unavailable.</summary>
    internal string GetLegacyKepubCacheFilePath(int chapterId, string fingerprint) =>
        _cacheStore.GetLegacyKepubCacheFilePath(_cacheStore.GetDefaultCacheRoot(), chapterId, fingerprint);

    /// <summary>Test seam over <see cref="KoboConversionCacheStore.FormatIdNameFolder"/>.</summary>
    internal static string FormatIdNameFolder(int id, string? name) =>
        KoboConversionCacheStore.FormatIdNameFolder(id, name);

    internal async Task<string> ResolveCacheRootAsync(CancellationToken ct = default)
    {
        try
        {
            var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
            return ResolveCacheRoot(settings);
        }
        catch
        {
            // Unit tests may substitute IUnitOfWork without settings.
            return _cacheStore.GetDefaultCacheRoot();
        }
    }

    internal string ResolveCacheRoot(Kavita.Models.DTOs.Settings.ServerSettingDto settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.KoboConversionCacheDirectory))
        {
            return settings.KoboConversionCacheDirectory;
        }

        return _cacheStore.GetDefaultCacheRoot();
    }

    private static bool NeedsLibraryWarmup(Chapter chapter, bool kepubEnabled)
    {
        var nativeEpub = KoboEligibleFormats.PreferNativeEpub(chapter.Files);
        var archive = KoboEligibleFormats.PreferConvertibleArchive(chapter.Files);
        if (nativeEpub == null && archive == null) return false;

        // Native EPUB chapters are only warmed when KEPUB production is enabled and not already kepub.
        if (nativeEpub != null)
        {
            return kepubEnabled && !IsAlreadyKepubLibraryFile(nativeEpub);
        }

        // Always warm archive→EPUB for CBZ/CBR without a native EPUB.
        return true;
    }

    private async Task ConvertChapterIfNeededAsync(Chapter chapter, CancellationToken ct)
    {
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        var cacheRoot = ResolveCacheRoot(settings);
        var identity = CacheIdentityFromChapter(chapter) ??
                       await TryResolveCacheIdentityAsync(chapter.Id, ct);
        var nativeEpub = KoboEligibleFormats.PreferNativeEpub(chapter.Files);
        var archive = KoboEligibleFormats.PreferConvertibleArchive(chapter.Files);
        var source = nativeEpub ?? archive;
        if (source == null)
        {
            logger.LogWarning("Background Kobo convert: chapter {ChapterId} has no EPUB/CBZ/CBR source",
                chapter.Id);
            return;
        }

        if (IsAlreadyKepubLibraryFile(source))
        {
            logger.LogDebug(
                "Background Kobo convert skipped; chapter {ChapterId} library file is already KEPUB",
                chapter.Id);
            return;
        }

        var title = chapter.Volume?.Series == null
            ? source.FileName
            : KoboEntitlementPayloadBuilder.BuildTitle(chapter.Volume.Series, chapter);

        // Archive → EPUB when there is no native EPUB.
        if (nativeEpub == null && archive != null)
        {
            var fingerprint = KoboConversionCacheStore.ComputeFingerprint(archive);
            if (TryGetCachedPath(cacheRoot, identity, chapter.Id, fingerprint, chapter.Pages) == null)
            {
                await ConvertAndCacheEpubAsync(cacheRoot, identity, chapter.Id, archive, title, fingerprint,
                    chapter.Pages, ct);
            }
        }

        if (!settings.EnableKepubConversion) return;
        var kepubifyPath = kepubifyPathResolver.Resolve(settings.KepubifyPath);
        if (kepubifyPath == null)
        {
            logger.LogWarning(
                "Background Kobo KEPUB convert skipped for chapter {ChapterId}: kepubify binary not found",
                chapter.Id);
            return;
        }

        if (await TryGetCachedKepubPathAsync(chapter.Id, source, ct) != null)
        {
            EnqueuePromoteIfNeeded(chapter.Id, source, settings);
            return;
        }

        await ConvertAndCacheKepubAsync(cacheRoot, identity, chapter.Id, source, title, kepubifyPath, ct);
    }

    private string? TryGetCachedPath(string cacheRoot, KoboCacheIdentity? identity, int chapterId,
        string fingerprint, int? expectedPages)
    {
        var path = ResolveArtifactPath(cacheRoot, identity, chapterId, fingerprint, isKepub: false);
        return _cacheStore.TouchIfValidCache(path, chapterId, expectedPages, "EPUB");
    }

    private string ResolveArtifactPath(string cacheRoot, KoboCacheIdentity? identity, int chapterId,
        string fingerprint, bool isKepub)
    {
        if (identity is { } id)
        {
            return _cacheStore.ResolveCacheFilePath(cacheRoot, id, fingerprint, isKepub);
        }

        return isKepub
            ? _cacheStore.GetLegacyKepubCacheFilePath(cacheRoot, chapterId, fingerprint)
            : _cacheStore.GetLegacyCacheFilePath(cacheRoot, chapterId, fingerprint);
    }

    private async Task<KoboCacheIdentity?> TryResolveCacheIdentityAsync(int chapterId, CancellationToken ct)
    {
        try
        {
            var context = unitOfWork.DataContext;
            if (context?.Chapter == null) return null;

            var row = await context.Chapter
                .AsNoTracking()
                .Where(c => c.Id == chapterId)
                .Select(c => new
                {
                    c.Id,
                    SeriesId = c.Volume.SeriesId,
                    SeriesName = c.Volume.Series.Name,
                    LibraryId = c.Volume.Series.LibraryId,
                    LibraryName = c.Volume.Series.Library.Name
                })
                .FirstOrDefaultAsync(ct);

            if (row == null) return null;

            return new KoboCacheIdentity(row.LibraryId, row.LibraryName, row.SeriesId, row.SeriesName, row.Id);
        }
        catch
        {
            // Unit tests may substitute IUnitOfWork without an EF context.
            return null;
        }
    }

    private static KoboCacheIdentity? CacheIdentityFromChapter(Chapter chapter)
    {
        var series = chapter.Volume?.Series;
        if (series == null) return null;

        var libraryName = series.Library?.Name ?? string.Empty;
        return new KoboCacheIdentity(series.LibraryId, libraryName, series.Id, series.Name, chapter.Id);
    }

    private async Task<int?> GetChapterPagesAsync(int chapterId, CancellationToken ct)
    {
        return await unitOfWork.DataContext.Chapter
            .AsNoTracking()
            .Where(c => c.Id == chapterId)
            .Select(c => (int?)c.Pages)
            .FirstOrDefaultAsync(ct);
    }

    private int? TryGetChapterPages(int chapterId)
    {
        try
        {
            var context = unitOfWork.DataContext;
            if (context?.Chapter == null) return null;
            return context.Chapter
                .AsNoTracking()
                .Where(c => c.Id == chapterId)
                .Select(c => (int?)c.Pages)
                .FirstOrDefault();
        }
        catch
        {
            // Unit tests may substitute IUnitOfWork without an EF context.
            return null;
        }
    }

    /// <summary>
    /// Write-path guard: refuses to cache an archive convert whose spine does not match chapter.Pages.
    /// </summary>
    private void EnsureSpineMatchesChapterPages(string epubPath, int chapterId, int? expectedPages, string poolLabel)
    {
        KoboConversionCacheStore.ValidateSpinePageCount(epubPath, expectedPages, spinePages =>
        {
            logger.LogError(
                "Kobo {Pool} convert page-count mismatch for chapter {ChapterId}: spine={SpinePages}, chapter.Pages={ChapterPages}. Refusing cache write for {Path}",
                poolLabel, chapterId, spinePages, expectedPages!.Value, epubPath);
            throw new KavitaException(ConvertFailedMessage);
        });
    }

    private async Task<string> ResolveEpubInputPathAsync(string cacheRoot, KoboCacheIdentity? identity,
        int chapterId, MangaFile sourceFile, string title, CancellationToken ct)
    {
        if (sourceFile.Format == MangaFormat.Epub)
        {
            return sourceFile.FilePath;
        }

        var expectedPages = await GetChapterPagesAsync(chapterId, ct);
        var fingerprint = KoboConversionCacheStore.ComputeFingerprint(sourceFile);
        var cached = TryGetCachedPath(cacheRoot, identity, chapterId, fingerprint, expectedPages);
        if (cached != null) return cached;

        return await ConvertAndCacheEpubAsync(cacheRoot, identity, chapterId, sourceFile, title, fingerprint,
            expectedPages, ct);
    }

    private async Task<string> ConvertAndCacheEpubAsync(string cacheRoot, KoboCacheIdentity? identity,
        int chapterId, MangaFile archiveFile, string title, string fingerprint, int? expectedPages,
        CancellationToken ct)
    {
        identity ??= await TryResolveCacheIdentityAsync(chapterId, ct);
        var finalPath = ResolveArtifactPath(cacheRoot, identity, chapterId, fingerprint, isKepub: false);
        var cacheDir = Path.GetDirectoryName(finalPath)!;

        await WriteCacheArtifactAsync(cacheDir, finalPath,
            (tempPath, token) => archiveEpubConverter.ConvertAsync(archiveFile.FilePath, tempPath, title, token),
            tempPath => EnsureSpineMatchesChapterPages(tempPath, chapterId, expectedPages, "EPUB"),
            isKepubPool: false, ct);

        await EnforceEpubCapAfterWriteAsync(cacheRoot, finalPath, ct);
        return finalPath;
    }

    private async Task<string> ConvertAndCacheKepubAsync(string cacheRoot, KoboCacheIdentity? identity,
        int chapterId, MangaFile sourceFile, string title, string kepubifyPath, CancellationToken ct)
    {
        identity ??= await TryResolveCacheIdentityAsync(chapterId, ct);
        var expectedPages = await GetChapterPagesAsync(chapterId, ct);
        var fingerprint = KoboConversionCacheStore.ComputeFingerprint(sourceFile);
        var finalPath = ResolveArtifactPath(cacheRoot, identity, chapterId, fingerprint, isKepub: true);
        var expectedForCache = sourceFile.Format == MangaFormat.Epub ? null : expectedPages;
        if (_cacheStore.TouchIfValidCache(finalPath, chapterId, expectedForCache, "KEPUB") != null)
        {
            await EnqueuePromoteIfNeededAsync(chapterId, sourceFile, ct);
            return finalPath;
        }

        var epubInput = await ResolveEpubInputPathAsync(cacheRoot, identity, chapterId, sourceFile, title, ct);
        ct.ThrowIfCancellationRequested();

        var cacheDir = Path.GetDirectoryName(finalPath)!;

        await WriteCacheArtifactAsync(cacheDir, finalPath,
            (tempPath, token) => kepubifyRunner.ConvertAsync(kepubifyPath, epubInput, tempPath, token),
            tempPath =>
            {
                // Archive converts must match chapter.Pages; native EPUB KEPUB keeps source spine as-is.
                if (sourceFile.Format != MangaFormat.Epub)
                {
                    EnsureSpineMatchesChapterPages(tempPath, chapterId, expectedPages, "KEPUB");
                }
            },
            isKepubPool: true, ct);

        await DropSyncedSetForChapterAsync(chapterId, ct);
        await koboLocationRematchService.RematchAfterDeviceFileChangeAsync(chapterId, finalPath, ct);
        await EnforceKepubCapAfterWriteAsync(cacheRoot, finalPath, ct);
        await EnqueuePromoteIfNeededAsync(chapterId, sourceFile, ct);
        return finalPath;
    }

    /// <summary>
    /// Produces an artifact into a <c>.partial</c> sibling, validates it, atomically moves it into place,
    /// touches it for LRU, and sweeps stale same-pool fingerprints for the chapter. Always cleans the temp file.
    /// </summary>
    private async Task WriteCacheArtifactAsync(string cacheDir, string finalPath,
        Func<string, CancellationToken, Task> produce, Action<string> validate, bool isKepubPool,
        CancellationToken ct)
    {
        directoryService.ExistOrCreate(cacheDir);

        var tempPath = finalPath + ".partial";
        try
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            await produce(tempPath, ct);
            ct.ThrowIfCancellationRequested();

            validate(tempPath);

            if (File.Exists(finalPath)) File.Delete(finalPath);
            File.Move(tempPath, finalPath);
            KoboConversionCacheStore.TouchIfExists(finalPath);

            _cacheStore.DeleteStaleFingerprints(cacheDir, finalPath, isKepubPool);
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

    private async Task EnforceEpubCapAfterWriteAsync(string cacheRoot, string justWrittenPath, CancellationToken ct)
    {
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        _cacheStore.EnforceEpubCap(cacheRoot, settings.KoboEpubCacheMaxBytes, justWrittenPath);
    }

    private async Task EnforceKepubCapAfterWriteAsync(string cacheRoot, string justWrittenPath, CancellationToken ct)
    {
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        _cacheStore.EnforceKepubCap(cacheRoot, settings.KoboKepubCacheMaxBytes, justWrittenPath);
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

    /// <summary>
    /// True when <paramref name="file"/> is a native library EPUB already named <c>*.kepub.epub</c>.
    /// </summary>
    public static bool IsAlreadyKepubLibraryFile(MangaFile file)
    {
        if (file is not { Format: MangaFormat.Epub } || string.IsNullOrWhiteSpace(file.FilePath))
        {
            return false;
        }

        return file.FilePath.EndsWith(KepubCacheExtension, StringComparison.OrdinalIgnoreCase);
    }

    private void EnqueuePromoteIfNeeded(int chapterId, MangaFile sourceFile,
        Kavita.Models.DTOs.Settings.ServerSettingDto settings)
    {
        if (!settings.ReplaceEpubWithKepub) return;
        if (!settings.EnableKepubConversion) return;
        if (sourceFile.Format != MangaFormat.Epub) return;
        if (IsAlreadyKepubLibraryFile(sourceFile)) return;

        jobScheduler.EnqueuePromoteKepubToLibrary(chapterId);
    }

    private async Task EnqueuePromoteIfNeededAsync(int chapterId, MangaFile sourceFile, CancellationToken ct)
    {
        try
        {
            var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
            EnqueuePromoteIfNeeded(chapterId, sourceFile, settings);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex,
                "Unable to evaluate ReplaceEpubWithKepub for chapter {ChapterId}; skipping promote enqueue",
                chapterId);
        }
    }

    /// <inheritdoc />
    public async Task PromoteKepubToLibraryAsync(int chapterId, CancellationToken ct = default)
    {
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        if (!settings.ReplaceEpubWithKepub || !settings.EnableKepubConversion)
        {
            return;
        }

        Chapter? chapter;
        try
        {
            chapter = await unitOfWork.DataContext.Chapter
                .Include(c => c.Files)
                .FirstOrDefaultAsync(c => c.Id == chapterId, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Promote KEPUB: unable to load chapter {ChapterId}", chapterId);
            return;
        }

        if (chapter == null)
        {
            logger.LogWarning("Promote KEPUB: chapter {ChapterId} not found", chapterId);
            return;
        }

        var source = KoboEligibleFormats.PreferNativeEpub(chapter.Files);
        if (source == null)
        {
            logger.LogDebug("Promote KEPUB skipped for chapter {ChapterId}: no native EPUB", chapterId);
            return;
        }

        if (IsAlreadyKepubLibraryFile(source))
        {
            logger.LogDebug("Promote KEPUB skipped for chapter {ChapterId}: already KEPUB", chapterId);
            return;
        }

        var cachedKepub = await TryGetCachedKepubPathAsync(chapterId, source, ct);
        if (cachedKepub == null || !File.Exists(cachedKepub))
        {
            logger.LogDebug(
                "Promote KEPUB skipped for chapter {ChapterId}: no cached KEPUB available", chapterId);
            return;
        }

        if (!ValidateKepubForPromotion(cachedKepub))
        {
            logger.LogWarning(
                "Promote KEPUB aborted for chapter {ChapterId}: cached KEPUB failed validation at {Path}",
                chapterId, cachedKepub);
            return;
        }

        var originalPath = source.FilePath;
        var libraryDir = Path.GetDirectoryName(originalPath);
        if (string.IsNullOrWhiteSpace(libraryDir))
        {
            logger.LogWarning(
                "Promote KEPUB aborted for chapter {ChapterId}: cannot resolve library directory for {Path}",
                chapterId, originalPath);
            return;
        }

        var stem = Path.GetFileNameWithoutExtension(originalPath);
        var targetPath = Parser.NormalizePath(Path.Combine(libraryDir, stem + KepubCacheExtension));
        if (string.Equals(Parser.NormalizePath(originalPath), targetPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var partialPath = targetPath + ".partial";
        try
        {
            if (File.Exists(partialPath)) File.Delete(partialPath);
            File.Copy(cachedKepub, partialPath, overwrite: true);

            if (!ValidateKepubForPromotion(partialPath))
            {
                logger.LogWarning(
                    "Promote KEPUB aborted for chapter {ChapterId}: copied KEPUB failed validation",
                    chapterId);
                TryDelete(partialPath);
                return;
            }

            if (File.Exists(targetPath)) File.Delete(targetPath);
            File.Move(partialPath, targetPath);

            // New file is safely in place; remove the original EPUB.
            if (!string.Equals(Parser.NormalizePath(originalPath), targetPath, StringComparison.OrdinalIgnoreCase)
                && File.Exists(originalPath))
            {
                File.Delete(originalPath);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Promote KEPUB failed during file swap for chapter {ChapterId} ({Original} -> {Target})",
                chapterId, originalPath, targetPath);
            TryDelete(partialPath);
            return;
        }

        try
        {
            var fileInfo = new FileInfo(targetPath);
            source.FilePath = targetPath;
            source.FileName = Parser.RemoveExtensionIfSupported(targetPath) ?? stem;
            source.Extension = fileInfo.Extension.ToLowerInvariant();
            source.Bytes = fileInfo.Length;
            source.LastModified = fileInfo.LastWriteTime;
            source.LastModifiedUtc = fileInfo.LastWriteTimeUtc;
            source.KoreaderHash = KoreaderHelper.HashContents(targetPath);

            unitOfWork.MangaFileRepository.Update(source);
            await unitOfWork.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Promote KEPUB: file swap succeeded for chapter {ChapterId} but DB update failed. Path is now {Target}; scanner will self-heal.",
                chapterId, targetPath);
            // Do not roll back the file swap — scanner will reconcile on next pass.
        }

        TryDelete(cachedKepub);

        try
        {
            await DropSyncedSetForChapterAsync(chapterId, ct);
            await koboLocationRematchService.RematchAfterDeviceFileChangeAsync(chapterId, targetPath, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Promote KEPUB: post-promote rematch/synced-set cleanup failed for chapter {ChapterId}",
                chapterId);
        }

        logger.LogInformation(
            "Promoted KEPUB into library for chapter {ChapterId}: {Original} -> {Target}",
            chapterId, originalPath, targetPath);
    }

    private static bool ValidateKepubForPromotion(string kepubPath)
    {
        try
        {
            var info = new FileInfo(kepubPath);
            if (!info.Exists || info.Length <= 0) return false;
            // Spine readable implies valid zip + package.
            return KoboConvertEpubInspector.TryCountSpinePages(kepubPath) is > 0;
        }
        catch
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // ignore cleanup races
        }
    }
}
