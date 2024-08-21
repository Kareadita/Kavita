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
    /// Allows Books to Scrobble with AniList for Kavita+
    /// </summary>
    [Description("Light Novel")]
    LightNovel = 4,
    /// <summary>
    /// Uses Comic regex for filename parsing, uses Comic Vine type of Parsing. Will replace Comic type in future
    /// </summary>
    [Description("Comic (Comic Vine)")]
    ComicVine = 5,
}
