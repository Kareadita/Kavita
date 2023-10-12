using System.ComponentModel;

namespace API.DTOs.Recommendation;

public enum PlusMediaFormat
{
    [Description("Manga")]
    Manga = 1,
    [Description("Comic")]
    Comic = 2,
    [Description("LightNovel")]
    LightNovel = 3,
    [Description("Book")]
    Book = 4
}
