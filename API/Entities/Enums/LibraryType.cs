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
        [Description("Images (Manga)")]
        MangaImages = 3,
        [Description("Images (Comic)")]
        ComicImages = 4
    }
}
