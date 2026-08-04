using System.IO;

namespace Kavita.Services.Kobo;

/// <summary>
/// Trusts a convert KEPUB only when the spine page count matches <c>Chapter.Pages</c>.
/// </summary>
public static class KoboTrustedKepubResolver
{
    public static bool IsTrusted(string path, int chapterPages)
    {
        if (chapterPages <= 0 || string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
        var spine = KoboConvertEpubInspector.TryCountSpinePages(path);
        return spine == chapterPages;
    }
}
