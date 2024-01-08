using System.ComponentModel;

namespace API.Entities.Enums;

public enum LibraryType
{
    /// <summary>
    /// Uses Manga regex for filename parsing
    /// </summary>
    [Description("Manga")]
    Manga = 0,
    /// <summary>
    /// Uses Comic regex for filename parsing
    /// </summary>
    [Description("Comic")]
    Comic = 1,
    /// <summary>
    /// Uses Manga regex for filename parsing also uses epub metadata
    /// </summary>
    [Description("Book")]
    Book = 2,
    /// <summary>
    /// Uses a different type of grouping and parsing mechanism
    /// </summary>
    [Description("Image")]
    Image = 3,
    /// <summary>
    /// Uses Magazine regex and is restricted to PDF and Archive by default
    /// </summary>
    [Description("Magazine")]
    Magazine = 4
}
