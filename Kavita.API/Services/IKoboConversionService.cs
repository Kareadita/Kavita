using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Kavita.Models.Entities;

namespace Kavita.API.Services;

/// <summary>
/// Shared CBZ/CBR → EPUB and EPUB → KEPUB conversion cache for Kobo download, sync queue, and library warm-up.
/// </summary>
public interface IKoboConversionService
{
    /// <summary>
    /// Returns a cached converted EPUB path, converting in-request up to <paramref name="budgetSeconds"/> on miss.
    /// </summary>
    /// <exception cref="Kavita.Common.KavitaException">
    /// <c>kobo-convert-unavailable</c> when over budget or already in flight;
    /// <c>kobo-convert-failed</c> on hard failure.
    /// </exception>
    Task<string> GetOrConvertEpubAsync(int chapterId, MangaFile archiveFile, string title,
        int budgetSeconds, CancellationToken ct = default);

    /// <summary>
    /// Returns a cached KEPUB path, converting in-request up to <paramref name="budgetSeconds"/> on miss
    /// (native EPUB or archive→EPUB cache, then kepubify). Requires Enable KEPUB + kepubify path.
    /// </summary>
    /// <exception cref="Kavita.Common.KavitaException">
    /// <c>kobo-convert-unavailable</c> when over budget or already in flight;
    /// <c>kobo-convert-failed</c> on hard failure;
    /// <c>kobo-format-unsupported</c> when KEPUB conversion is disabled.
    /// </exception>
    Task<string> GetOrConvertKepubAsync(int chapterId, MangaFile sourceFile, string title,
        int budgetSeconds, CancellationToken ct = default);

    /// <summary>
    /// Returns a cached KEPUB path for the chapter source fingerprint, or null when missing.
    /// Fingerprint is derived from <paramref name="sourceFile"/> (native EPUB or convertible archive).
    /// </summary>
    Task<string?> TryGetCachedKepubPathAsync(int chapterId, MangaFile sourceFile,
        CancellationToken ct = default);

    /// <summary>
    /// When KEPUB conversion is enabled and no cached KEPUB exists for <paramref name="sourceFile"/>,
    /// enqueues a background convert (archive→EPUB if needed, then kepubify).
    /// When a cache hit exists and Replace EPUB with KEPUB is on, enqueues promotion instead.
    /// </summary>
    Task EnqueueKepubifyIfNeededAsync(int chapterId, MangaFile sourceFile, CancellationToken ct = default);

    /// <summary>
    /// Background convert into the shared cache without the in-request time budget.
    /// Produces archive→EPUB when needed and KEPUB when enabled.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60)]
    Task ConvertChapterInBackgroundAsync(int chapterId, CancellationToken ct = default);

    /// <summary>
    /// When Replace EPUB with KEPUB is enabled, promotes a cached KEPUB into the library folder
    /// in place of the original native EPUB (updates MangaFile, drops cache copy).
    /// No-op for archive sources, already-kepub files, or when the setting is off.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 10)]
    Task PromoteKepubToLibraryAsync(int chapterId, CancellationToken ct = default);

    /// <summary>
    /// Warms the shared conversion cache for a library (no in-request budget).
    /// Always converts CBZ/CBR → EPUB; when KEPUB is enabled also produces KEPUB for native EPUB and archives.
    /// Reports SignalR progress (cover-gen style).
    /// </summary>
    Task ConvertLibraryForKoboAsync(int libraryId, CancellationToken ct = default);

    /// <summary>
    /// Deletes all files under the shared Kobo conversion cache.
    /// When admin byte caps are set, LRU eviction also runs on write and via periodic cleanup.
    /// </summary>
    Task ClearConversionCacheAsync(CancellationToken ct = default);

    /// <summary>
    /// Enforces configured archive→EPUB and EPUB→KEPUB byte caps by deleting least-recently-accessed
    /// artifacts until each pool is under budget. No-op when a cap is unset/unlimited.
    /// </summary>
    Task EnforceConversionCacheCapsAsync(CancellationToken ct = default);
}
