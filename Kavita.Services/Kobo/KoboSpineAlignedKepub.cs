using System.IO;

namespace Kavita.Services.Kobo;

/// <summary>
/// A convert KEPUB is spine-aligned when its OPF spine length matches <c>Chapter.Pages</c>.
/// Location encode/decode requires this alignment.
/// </summary>
public static class KoboSpineAlignedKepub
{
    public static bool IsSpineAligned(string path, int chapterPages)
    {
        if (chapterPages <= 0 || string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
        var spine = KoboConvertEpubInspector.TryCountSpinePages(path);
        return spine == chapterPages;
    }
}
