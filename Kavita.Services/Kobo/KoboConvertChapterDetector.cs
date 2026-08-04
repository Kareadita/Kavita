using System;
using Kavita.Models.Entities;

namespace Kavita.Services.Kobo;

/// <summary>
/// Detects chapters that sync via CBZ/CBR→EPUB/KEPUB conversion (no native EPUB).
/// </summary>
public static class KoboConvertChapterDetector
{
    public static bool IsConvertChapter(Chapter chapter)
    {
        ArgumentNullException.ThrowIfNull(chapter);
        return KoboEligibleFormats.PreferNativeEpub(chapter.Files) == null
               && KoboEligibleFormats.PreferConvertibleArchive(chapter.Files) != null;
    }
}
