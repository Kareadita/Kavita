using System.ComponentModel;

namespace API.Entities
{
    public enum LibraryType
    {
        [Description("Manga")]
        Manga = 0,
        [Description("Comic")]
        Comic = 1,
        [Description("Book")]
        Book = 2,
        [Description("Webtoon")]
        Webtoon = 3
    }
}