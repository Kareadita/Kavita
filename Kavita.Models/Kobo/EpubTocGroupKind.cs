using System.ComponentModel;

namespace Kavita.Models.Kobo;

public enum EpubTocGroupKind
{
    /// <summary>
    /// Pages from files that share a chapter number parsed from their file names
    /// </summary>
    [Description("Chapter")]
    Chapter = 0,
    /// <summary>
    /// Fixed-size run of pages, used when nothing better was found
    /// </summary>
    [Description("Pages")]
    Pages = 1,
    /// <summary>
    /// A single page, used for short chapters with no chapter grouping
    /// </summary>
    [Description("Page")]
    Page = 2,
}
