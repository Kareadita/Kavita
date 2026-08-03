using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.Entities;

namespace Kavita.API.Services;

/// <summary>
/// Best-effort map between web <c>BookScrollId</c> (descoped XPath) and Kobo
/// <c>CurrentBookmark.Location</c>. Updates only when the result is valid in the target file.
/// </summary>
public interface IKoboLocationMapper
{
    /// <summary>
    /// Maps Location → BookScrollId when Value resolves in the library EPUB. Null on failure.
    /// Never invents markers; caller leaves prior BookScrollId unchanged when null.
    /// </summary>
    Task<string?> TryMapLocationToBookScrollIdAsync(string? libraryEpubPath,
        string? locationValue, string? locationType, string? locationSource,
        CancellationToken ct = default);

    /// <summary>
    /// Maps BookScrollId → Location when a real in-file id (typically <c>kobo.N.M</c>) is found.
    /// Null on failure — never invents KoboSpan ids absent from the device-openable file.
    /// </summary>
    Task<KoboMappedLocation?> TryMapBookScrollIdToLocationAsync(string? deviceOpenablePath,
        int pageNum, string? bookScrollId, CancellationToken ct = default);

    /// <summary>
    /// Native library EPUB path for web validation, or null when the chapter has no native EPUB
    /// (e.g. CBZ/CBR-only — percent-only exact position).
    /// </summary>
    string? ResolveLibraryEpubPath(Chapter chapter);

    /// <summary>
    /// Device-openable EPUB/KEPUB path for Location validation. Prefers cached KEPUB when present;
    /// otherwise native EPUB. Null for archive-only chapters (no Location invent from converts).
    /// </summary>
    string? ResolveDeviceOpenablePath(Chapter chapter, bool preferKepubWhenCached);
}

/// <summary>Mapped Kobo Location fields.</summary>
public sealed record KoboMappedLocation(string Value, string Type, string Source);
