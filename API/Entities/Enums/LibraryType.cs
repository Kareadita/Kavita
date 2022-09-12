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
}
