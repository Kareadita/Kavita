using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.Entities;

namespace Kavita.API.Services;

/// <summary>
/// Shared CBZ/CBR → EPUB conversion cache for Kobo download and whole-library warm-up.
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
    /// Background convert into the shared cache without the in-request time budget.
    /// </summary>
    Task ConvertChapterInBackgroundAsync(int chapterId, CancellationToken ct = default);

    /// <summary>
    /// Warms the shared conversion cache for all CBZ/CBR-only chapters in a library (no in-request budget).
    /// Reports SignalR progress (cover-gen style).
    /// </summary>
    Task ConvertLibraryForKoboAsync(int libraryId, CancellationToken ct = default);

    /// <summary>
    /// Deletes all files under the shared Kobo conversion cache. No automatic LRU.
    /// </summary>
    Task ClearConversionCacheAsync(CancellationToken ct = default);
}
