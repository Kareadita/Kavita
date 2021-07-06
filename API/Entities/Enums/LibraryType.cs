using System.ComponentModel;

namespace API.Entities.Enums
{
    public enum LibraryType
    {
        [Description("Manga")]
        Manga = 0,
        [Description("Comic")]
        Comic = 1,
        [Description("Book")]
        Book = 2,
        [Description("Images (Comic)")]
        MangaImages = 3,
        [Description("Images (Manga)")]
        ComicImages = 4
    }
}
