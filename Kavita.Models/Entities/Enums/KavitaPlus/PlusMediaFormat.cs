using System.ComponentModel;

namespace Kavita.Models.Entities.Enums.KavitaPlus;

/// <summary>
/// Represents PlusMediaFormat
/// </summary>
public enum PlusMediaFormat
{
    [Description("Manga")]
    Manga = 1,
    [Description("Comic")]
    Comic = 2,
    [Description("LightNovel")]
    LightNovel = 3,
    [Description("Book")]
    Book = 4,
    Unknown = 5
}
