using System.ComponentModel;

namespace Kavita.Models.Kobo;

public enum EpubTocGroupKind
{
    /// <summary>
    /// Pages share a chapter number parsed from their file names
    /// </summary>
    [Description("Chapter")]
    Chapter = 0,
    /// <summary>
    /// Pages came from the same folder inside the archive
    /// </summary>
    [Description("Folder")]
    Folder = 1,
    /// <summary>
    /// Fixed-size run of pages, used when nothing better was found
    /// </summary>
    [Description("Pages")]
    Pages = 2,
}
