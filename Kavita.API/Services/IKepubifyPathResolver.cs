namespace Kavita.API.Services;

/// <summary>
/// Resolves the kepubify binary path for KEPUB conversion (admin override, bundled, or PATH).
/// </summary>
public interface IKepubifyPathResolver
{
    /// <summary>
    /// Returns an absolute path to a kepubify binary, or null if none is available.
    /// Order: non-empty <paramref name="configuredPath"/> when the file exists;
    /// bundled <c>tools/kepubify</c> next to the app; then <c>kepubify</c> on PATH.
    /// </summary>
    string? Resolve(string? configuredPath);
}
